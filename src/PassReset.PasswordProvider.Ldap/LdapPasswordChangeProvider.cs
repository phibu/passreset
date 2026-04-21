using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PassReset.Common;

namespace PassReset.PasswordProvider.Ldap;

/// <summary>
/// Cross-platform <see cref="IPasswordChangeProvider"/> backed by
/// <see cref="System.DirectoryServices.Protocols.LdapConnection"/>. Runs on Windows, Linux, and macOS.
/// Behavioral parity with the Windows provider is enforced by the shared
/// <c>IPasswordChangeProviderContract</c> test suite.
/// </summary>
public sealed class LdapPasswordChangeProvider : IPasswordChangeProvider
{
    private const string UserObjectClassFilter = "(objectClass=user)";

    private readonly IOptions<PasswordChangeOptions> _options;
    private readonly ILogger<LdapPasswordChangeProvider> _logger;
    private readonly Func<ILdapSession> _sessionFactory;

    public LdapPasswordChangeProvider(
        IOptions<PasswordChangeOptions> options,
        ILogger<LdapPasswordChangeProvider> logger,
        Func<ILdapSession> sessionFactory)
    {
        _options = options;
        _logger = logger;
        _sessionFactory = sessionFactory;

        if (OperatingSystem.IsWindows())
        {
            _logger.LogInformation(
                "LdapPasswordChangeProvider active on Windows (ProviderMode={Mode}). " +
                "UserCannotChangePassword ACE check is Linux-deferred; AD server-side enforcement applies.",
                _options.Value.ProviderMode);
        }
    }

    /// <summary>
    /// Resolves <paramref name="username"/> to its distinguished name by searching each
    /// attribute in <see cref="PasswordChangeOptions.AllowedUsernameAttributes"/> in order.
    /// Returns null when no attribute matches.
    /// </summary>
    internal async Task<string?> FindUserDnAsync(ILdapSession session, string username)
    {
        await Task.Yield();  // reserved for future async LDAP APIs
        var opts = _options.Value;
        foreach (var attr in opts.AllowedUsernameAttributes)
        {
            var ldapAttr = attr.ToLowerInvariant() switch
            {
                "samaccountname"    => LdapAttributeNames.SamAccountName,
                "userprincipalname" => LdapAttributeNames.UserPrincipalName,
                "mail"              => LdapAttributeNames.Mail,
                _ => null,
            };
            if (ldapAttr is null)
            {
                _logger.LogWarning("Ignoring unknown AllowedUsernameAttributes entry: {Attr}", attr);
                continue;
            }

            var filter = $"({ldapAttr}={EscapeLdapFilterValue(username)})";
            var request = new SearchRequest(
                distinguishedName: opts.BaseDn,
                ldapFilter: filter,
                searchScope: SearchScope.Subtree,
                attributeList: new[] { LdapAttributeNames.DistinguishedName });
            var response = session.Search(request);

            if (response.Entries.Count == 1)
                return response.Entries[0].DistinguishedName;

            if (response.Entries.Count > 1)
            {
                _logger.LogWarning(
                    "Ambiguous match: {Count} entries for {Attr}={Username}. Treating as not found.",
                    response.Entries.Count, ldapAttr, username);
            }
        }
        return null;
    }

    /// <summary>
    /// RFC 4515 LDAP filter value escaping: backslash, asterisk, parenthesis, NUL.
    /// Prevents filter injection when user input is interpolated into a search filter.
    /// </summary>
    internal static string EscapeLdapFilterValue(string value) =>
        value
            .Replace("\\", @"\5c")
            .Replace("*",  @"\2a")
            .Replace("(",  @"\28")
            .Replace(")",  @"\29")
            .Replace("\0", @"\00");

    public async Task<ApiErrorItem?> PerformPasswordChangeAsync(string username, string currentPassword, string newPassword)
    {
        using var session = _sessionFactory();

        try
        {
            session.Bind();
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "LDAP bind failed as service account");
            return new ApiErrorItem(ApiErrorCode.Generic,
                "Directory bind failed; contact your administrator.");
        }

        var userDn = await FindUserDnAsync(session, username);
        if (userDn is null)
        {
            _logger.LogInformation("User not found: {Username}", username);
            return new ApiErrorItem(ApiErrorCode.UserNotFound,
                "User not found in directory.") { FieldName = nameof(username) };
        }

        var opts = _options.Value;

        // Group membership policy: deny if user is in any RestrictedAdGroups,
        // or if AllowedAdGroups is configured and the user is in none of them.
        var groupCheck = ValidateGroups(session, userDn);
        if (groupCheck is not null) return groupCheck;

        // STAB-004: defense-in-depth client-side check of domain minPwdAge against pwdLastSet.
        // Avoids the round-trip + opaque AD error that the server would return otherwise.
        var agePrecheck = PreCheckMinPwdAge(session, userDn);
        if (agePrecheck is not null) return agePrecheck;

        try
        {
            var modifyRequest = BuildChangePasswordRequest(userDn, currentPassword, newPassword, opts.AllowSetPasswordFallback);
            var response = session.Modify(modifyRequest);

            if (response.ResultCode != ResultCode.Success)
            {
                var extended = LdapErrorMapping.ExtractExtendedError(response.ErrorMessage);
                var mapped = LdapErrorMapping.Map(response.ResultCode, extended);
                _logger.LogWarning(
                    "ModifyResponse rejected: ResultCode={ResultCode} extendedError=0x{Extended:X8} mapped={Mapped}",
                    response.ResultCode, extended, mapped);
                return new ApiErrorItem(mapped, MessageFor(mapped));
            }

            return null;
        }
        catch (DirectoryOperationException ex)
        {
            var extended = LdapErrorMapping.ExtractExtendedError(ex.Response?.ErrorMessage);
            var mapped = LdapErrorMapping.Map(ex.Response?.ResultCode ?? ResultCode.OperationsError, extended);
            _logger.LogWarning(ex,
                "DirectoryOperationException on Modify: ResultCode={ResultCode} extendedError=0x{Extended:X8} mapped={Mapped}",
                ex.Response?.ResultCode, extended, mapped);
            return new ApiErrorItem(mapped, MessageFor(mapped));
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "Unexpected LDAP exception on password change");
            return new ApiErrorItem(ApiErrorCode.Generic, "Unexpected directory error.");
        }
    }

    private static ModifyRequest BuildChangePasswordRequest(
        string userDn, string current, string next, bool allowSetFallback)
    {
        // AD atomic change-password pattern: single ModifyRequest with Delete(old) + Add(new)
        // on unicodePwd. The value must be UTF-16LE-encoded and wrapped in literal quote chars.
        //
        // Memory hygiene note: these byte buffers contain the cleartext passwords as UTF-16
        // and CANNOT be zeroed here. DirectoryAttributeModification derives from
        // DirectoryAttribute, which derives from CollectionBase and stores items in an
        // ArrayList of object references. The Add(byte[]) overload boxes and stores the
        // array reference directly — it does NOT clone. Zeroing these buffers before
        // session.Modify(...) serializes the request would corrupt the wire payload.
        // The caller is expected to invoke Modify synchronously and not retain the request
        // beyond that call; the buffers become eligible for GC once the request goes out
        // of scope. (System.DirectoryServices.Protocols does not expose a clear-after-send
        // API — true zeroization would require avoiding managed string/byte[] entirely.)
        var oldBytes = System.Text.Encoding.Unicode.GetBytes($"\"{current}\"");
        var newBytes = System.Text.Encoding.Unicode.GetBytes($"\"{next}\"");

        if (allowSetFallback)
        {
            // Replace semantic: SetPassword equivalent — bypasses history. Opt-in only.
            var replace = new DirectoryAttributeModification
            {
                Operation = DirectoryAttributeOperation.Replace,
                Name = LdapAttributeNames.UnicodePwd,
            };
            replace.Add(newBytes);
            return new ModifyRequest(userDn, replace);
        }

        var del = new DirectoryAttributeModification
        {
            Operation = DirectoryAttributeOperation.Delete,
            Name = LdapAttributeNames.UnicodePwd,
        };
        del.Add(oldBytes);
        var add = new DirectoryAttributeModification
        {
            Operation = DirectoryAttributeOperation.Add,
            Name = LdapAttributeNames.UnicodePwd,
        };
        add.Add(newBytes);
        return new ModifyRequest(userDn, del, add);
    }

    /// <summary>
    /// Enforces RestrictedAdGroups (deny-list) and AllowedAdGroups (allow-list) membership policy.
    /// Skips the LDAP round-trip when neither list is configured. Compares the user's memberOf
    /// values by Common Name (CN=Foo,...) against the configured group names (case-insensitive).
    /// </summary>
    /// <returns>
    /// <see cref="ApiErrorItem"/> with <see cref="ApiErrorCode.ChangeNotPermitted"/> when the user
    /// is in a restricted group, or absent from a non-empty allowed list. <c>null</c> when allowed.
    /// </returns>
    private ApiErrorItem? ValidateGroups(ILdapSession session, string userDn)
    {
        var opts = _options.Value;
        var restricted = opts.RestrictedAdGroups ?? new List<string>();
        var allowed    = opts.AllowedAdGroups    ?? new List<string>();
        if (restricted.Count == 0 && allowed.Count == 0)
            return null;  // Nothing to enforce — skip the LDAP round-trip.

        var userGroups = ReadUserGroups(session, userDn);

        // Fail closed: if memberOf could not be read, we cannot evaluate either policy
        // safely. Deny-list bypass would be especially dangerous (user appears unrestricted
        // when in reality the lookup itself failed). Allow-list mode is also denied since
        // the user trivially appears in no allowed groups.
        if (userGroups is null)
        {
            _logger.LogError(
                "Change denied: memberOf lookup failed for {UserDn}; cannot evaluate group policy. " +
                "Verify the service account has read access to the user's memberOf attribute.",
                userDn);
            return new ApiErrorItem(ApiErrorCode.ChangeNotPermitted, MessageFor(ApiErrorCode.ChangeNotPermitted));
        }

        if (restricted.Count > 0)
        {
            foreach (var g in restricted)
            {
                if (userGroups.Contains(g, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Change denied: user {UserDn} is in restricted group {Group}", userDn, g);
                    return new ApiErrorItem(ApiErrorCode.ChangeNotPermitted, MessageFor(ApiErrorCode.ChangeNotPermitted));
                }
            }
        }

        if (allowed.Count > 0)
        {
            var anyAllowed = allowed.Any(g => userGroups.Contains(g, StringComparer.OrdinalIgnoreCase));
            if (!anyAllowed)
            {
                _logger.LogInformation(
                    "Change denied: user {UserDn} is not in any of the AllowedAdGroups", userDn);
                return new ApiErrorItem(ApiErrorCode.ChangeNotPermitted, MessageFor(ApiErrorCode.ChangeNotPermitted));
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the <c>memberOf</c> attribute for <paramref name="userDn"/> and extracts the CN of each
    /// distinguished name. Returns <c>null</c> on any LDAP failure so the caller can fail closed:
    /// a silent empty list would let the deny-list path bypass <c>RestrictedAdGroups</c> when the
    /// service account lacks read permission on <c>memberOf</c>. An empty (non-null) list means
    /// "lookup succeeded, user has no group memberships."
    /// </summary>
    private IReadOnlyList<string>? ReadUserGroups(ILdapSession session, string userDn)
    {
        try
        {
            var req = new SearchRequest(
                distinguishedName: userDn,
                ldapFilter: UserObjectClassFilter,
                searchScope: SearchScope.Base,
                attributeList: new[] { LdapAttributeNames.MemberOf });
            var resp = session.Search(req);
            if (resp.Entries.Count == 0) return Array.Empty<string>();
            var entry = resp.Entries[0];
            if (!entry.Attributes.Contains(LdapAttributeNames.MemberOf)) return Array.Empty<string>();
            var attr = entry.Attributes[LdapAttributeNames.MemberOf];
            var result = new List<string>(attr.Count);
            foreach (var raw in attr.GetValues(typeof(string)))
            {
                if (raw is string dn)
                {
                    var cn = ExtractCommonName(dn);
                    if (cn is not null) result.Add(cn);
                }
            }
            return result;
        }
        catch (Exception ex) when (ex is LdapException or DirectoryOperationException)
        {
            _logger.LogError(ex,
                "memberOf lookup failed for {UserDn}. Group policy cannot be evaluated; " +
                "the change request will be denied (fail-closed).", userDn);
            return null;
        }
    }

    /// <summary>
    /// Extracts the Common Name from a DN, e.g. <c>CN=Domain Admins,CN=Users,DC=corp,DC=example,DC=com</c>
    /// → <c>Domain Admins</c>. Honours RFC 4514 RDN escaping: backslash-escaped commas
    /// (<c>CN=Doe\, John,OU=…</c> → <c>Doe, John</c>) do not split the RDN, and escape sequences
    /// in the resulting CN are unescaped (<c>\,</c> → <c>,</c>, <c>\\</c> → <c>\</c>, <c>\=</c> → <c>=</c>, etc.).
    /// Returns <c>null</c> if the DN does not start with <c>CN=</c>.
    /// </summary>
    internal static string? ExtractCommonName(string distinguishedName)
    {
        if (string.IsNullOrEmpty(distinguishedName)) return null;
        const string prefix = "CN=";
        if (!distinguishedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        // Find the first UNESCAPED comma after the CN= prefix. A comma is unescaped when it is
        // preceded by an even number (including zero) of backslashes — odd means the prior
        // backslash is escaping the comma (RFC 4514 §2.4).
        var commaIdx = -1;
        for (var i = prefix.Length; i < distinguishedName.Length; i++)
        {
            if (distinguishedName[i] != ',') continue;
            var backslashes = 0;
            for (var j = i - 1; j >= prefix.Length && distinguishedName[j] == '\\'; j--) backslashes++;
            if (backslashes % 2 == 0) { commaIdx = i; break; }
        }

        var raw = commaIdx < 0
            ? distinguishedName[prefix.Length..]
            : distinguishedName[prefix.Length..commaIdx];

        return UnescapeRdnValue(raw);
    }

    /// <summary>
    /// Reverses RFC 4514 RDN value escaping: a backslash followed by any character is collapsed
    /// to that character (covers <c>\,</c>, <c>\\</c>, <c>\=</c>, <c>\+</c>, <c>\;</c>, <c>\&lt;</c>,
    /// <c>\&gt;</c>, <c>\"</c>, leading/trailing <c>\#</c>/<c>\ </c>). Hex-encoded escapes
    /// (<c>\NN</c>) are not currently un-encoded — AD's group DNs do not use them in practice.
    /// </summary>
    private static string UnescapeRdnValue(string value)
    {
        if (value.IndexOf('\\') < 0) return value;
        var sb = new System.Text.StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                sb.Append(value[i + 1]);
                i++;
            }
            else
            {
                sb.Append(value[i]);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// STAB-004 defense-in-depth: blocks a change request when <c>now - pwdLastSet</c>
    /// is shorter than the domain's <c>minPwdAge</c>. Skipped silently when
    /// <see cref="PasswordChangeOptions.EnforceMinimumPasswordAge"/> is false, when the
    /// rootDSE query returned no <c>minPwdAge</c>, or when the user has no <c>pwdLastSet</c>
    /// attribute (which would mean "must change at next logon" — let AD handle that path).
    /// </summary>
    private ApiErrorItem? PreCheckMinPwdAge(ILdapSession session, string userDn)
    {
        var opts = _options.Value;
        if (!opts.EnforceMinimumPasswordAge) return null;

        var rootDse = session.RootDse;
        if (rootDse is null || !rootDse.Attributes.Contains(LdapAttributeNames.MinPwdAge))
            return null;

        if (!long.TryParse(GetFirstStringValueOrNull(rootDse, LdapAttributeNames.MinPwdAge), out var minPwdAgeRaw))
            return null;

        // AD stores minPwdAge as a negative 100-ns interval (e.g. -864000000000 == 1 day).
        // Zero means no minimum age is enforced.
        if (minPwdAgeRaw == 0) return null;
        var minPwdAge = TimeSpan.FromTicks(Math.Abs(minPwdAgeRaw));

        // Read pwdLastSet for the user via Base-scope search.
        long pwdLastSetRaw;
        try
        {
            var req = new SearchRequest(
                distinguishedName: userDn,
                ldapFilter: UserObjectClassFilter,
                searchScope: SearchScope.Base,
                attributeList: new[] { LdapAttributeNames.PwdLastSet });
            var resp = session.Search(req);
            if (resp.Entries.Count == 0) return null;
            var entry = resp.Entries[0];
            if (!entry.Attributes.Contains(LdapAttributeNames.PwdLastSet)) return null;
            if (!long.TryParse(GetFirstStringValueOrNull(entry, LdapAttributeNames.PwdLastSet), out pwdLastSetRaw))
                return null;
        }
        catch (Exception ex) when (ex is LdapException or DirectoryOperationException)
        {
            _logger.LogWarning(ex, "pwdLastSet lookup failed for {UserDn}; skipping min-age precheck", userDn);
            return null;
        }

        // pwdLastSet=0 means "must change at next logon" — minPwdAge does not apply.
        if (pwdLastSetRaw == 0) return null;

        var lastSet = DateTime.FromFileTimeUtc(pwdLastSetRaw);
        var elapsed = DateTime.UtcNow - lastSet;
        if (elapsed < minPwdAge)
        {
            _logger.LogInformation(
                "Change blocked by minPwdAge precheck: elapsed={Elapsed} minAge={MinAge} for {UserDn}",
                elapsed, minPwdAge, userDn);
            return new ApiErrorItem(ApiErrorCode.PasswordTooRecentlyChanged,
                MessageFor(ApiErrorCode.PasswordTooRecentlyChanged));
        }
        return null;
    }

    private static string? GetFirstStringValueOrNull(SearchResultEntry entry, string attributeName)
    {
        if (!entry.Attributes.Contains(attributeName)) return null;
        var values = entry.Attributes[attributeName].GetValues(typeof(string));
        return values.Length > 0 ? values[0] as string : null;
    }

    private static string MessageFor(ApiErrorCode code) => code switch
    {
        ApiErrorCode.InvalidCredentials          => "Current password is incorrect.",
        ApiErrorCode.UserNotFound                => "User not found in directory.",
        ApiErrorCode.ChangeNotPermitted          => "Password change is not permitted for this account.",
        ApiErrorCode.ComplexPassword             => "The new password does not meet domain complexity requirements.",
        ApiErrorCode.PortalLockout               => "Account is locked out. Contact your administrator.",
        ApiErrorCode.PasswordTooRecentlyChanged  => "Password was changed too recently; please wait before trying again.",
        // Fail loud: any unmapped code from LdapErrorMapping.Map is a bug — we want it
        // surfaced in development and observable in logs, not silently masked.
        _ => throw new ArgumentOutOfRangeException(
            nameof(code), code, "No user-facing message defined for this error code"),
    };

    public string? GetUserEmail(string username)
        => throw new NotImplementedException();

    public IEnumerable<(string Username, string Email, DateTime? PasswordLastSet)> GetUsersInGroup(string groupName)
        => throw new NotImplementedException();

    public TimeSpan GetDomainMaxPasswordAge()
        => throw new NotImplementedException();

    public Task<PasswordPolicy?> GetEffectivePasswordPolicyAsync()
        => throw new NotImplementedException();
}
