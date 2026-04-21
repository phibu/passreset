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

    public Task<ApiErrorItem?> PerformPasswordChangeAsync(string username, string currentPassword, string newPassword)
        => throw new NotImplementedException();

    public string? GetUserEmail(string username)
        => throw new NotImplementedException();

    public IEnumerable<(string Username, string Email, DateTime? PasswordLastSet)> GetUsersInGroup(string groupName)
        => throw new NotImplementedException();

    public TimeSpan GetDomainMaxPasswordAge()
        => throw new NotImplementedException();

    public Task<PasswordPolicy?> GetEffectivePasswordPolicyAsync()
        => throw new NotImplementedException();
}
