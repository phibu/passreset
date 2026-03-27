using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PassReset.Common;

namespace PassReset.PasswordProvider;

/// <summary>
/// Decorator around <see cref="IPasswordChangeProvider"/> that tracks per-username
/// credential failure counts in an in-process memory cache and blocks requests before
/// they reach Active Directory once the portal lockout threshold is reached.
///
/// This prevents both accidental self-lockout (AD lockout from repeated typos) and
/// targeted lockout attacks (an attacker burning the AD bad-password quota for a victim).
/// The counter is keyed on the normalised username (SAM part only, lowercase), so it is
/// effective regardless of how many source IPs the caller rotates through.
///
/// Threshold semantics:
///   failures &lt; threshold        → request is passed through to the inner provider
///   failures == threshold - 1   → inner provider is called; on InvalidCredentials the
///                                  response is upgraded to <see cref="ApiErrorCode.ApproachingLockout"/>
///                                  to signal the UI to show a warning banner
///   failures &gt;= threshold       → <see cref="ApiErrorCode.PortalLockout"/> is returned
///                                  immediately without contacting AD
///
/// Set <see cref="PasswordChangeOptions.PortalLockoutThreshold"/> to 0 to disable.
/// </summary>
public sealed class LockoutPasswordChangeProvider : IPasswordChangeProvider
{
    private const string CacheKeyPrefix = "portal_lockout:";

    private readonly IPasswordChangeProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly PasswordChangeOptions _options;
    private readonly ILogger<LockoutPasswordChangeProvider> _logger;

    public LockoutPasswordChangeProvider(
        IPasswordChangeProvider inner,
        IMemoryCache cache,
        IOptions<PasswordChangeOptions> options,
        ILogger<LockoutPasswordChangeProvider> logger)
    {
        _inner   = inner;
        _cache   = cache;
        _options = options.Value;
        _logger  = logger;
    }

    /// <inheritdoc />
    public ApiErrorItem? PerformPasswordChange(string username, string currentPassword, string newPassword)
    {
        var threshold = _options.PortalLockoutThreshold;

        if (threshold > 0)
        {
            var key   = BuildCacheKey(username);
            var count = _cache.TryGetValue<int>(key, out var c) ? c : 0;

            if (count >= threshold)
            {
                _logger.LogWarning(
                    "Portal lockout active for {Username} — {Count}/{Threshold} failures in window, AD not contacted",
                    username, count, threshold);
                return new ApiErrorItem(ApiErrorCode.PortalLockout);
            }
        }

        var result = _inner.PerformPasswordChange(username, currentPassword, newPassword);

        if (threshold > 0)
        {
            var key = BuildCacheKey(username);

            if (result?.ErrorCode == ApiErrorCode.InvalidCredentials)
            {
                var newCount = IncrementCounter(key);

                _logger.LogWarning(
                    "Portal failure counter for {Username}: {Count}/{Threshold}",
                    username, newCount, threshold);

                // Warn on the attempt BEFORE lockout so the user knows to be careful.
                if (newCount == threshold - 1)
                    return new ApiErrorItem(ApiErrorCode.ApproachingLockout);
            }
            else if (result is null)
            {
                // Successful password change — reset the counter immediately.
                _cache.Remove(key);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public string? GetUserEmail(string username) => _inner.GetUserEmail(username);

    /// <inheritdoc />
    public IEnumerable<(string Username, string Email, DateTime? PasswordLastSet)>
        GetUsersInGroup(string groupName) => _inner.GetUsersInGroup(groupName);

    /// <inheritdoc />
    public TimeSpan GetDomainMaxPasswordAge() => _inner.GetDomainMaxPasswordAge();

    // ─── Private helpers ──────────────────────────────────────────────────────

    private int IncrementCounter(string key)
    {
        var current  = _cache.TryGetValue<int>(key, out var c) ? c : 0;
        var newCount = current + 1;
        _cache.Set(key, newCount, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.PortalLockoutWindow,
        });
        return newCount;
    }

    /// <summary>
    /// Normalises the username to a SAM-like key for consistent cache lookups
    /// regardless of how the caller formatted the input
    /// (bare: <c>jdoe</c>, UPN: <c>jdoe@corp.com</c>, NetBIOS: <c>CORP\jdoe</c>).
    /// Must stay in sync with <c>FindBySamAccountName</c> in <c>PasswordChangeProvider</c>.
    /// </summary>
    internal static string BuildCacheKey(string username)
    {
        var normalised = username.Trim().ToLowerInvariant();
        normalised = normalised.Contains('\\') ? normalised[(normalised.IndexOf('\\') + 1)..] :
                     normalised.Contains('@')  ? normalised[..normalised.IndexOf('@')]          :
                     normalised;
        return CacheKeyPrefix + normalised;
    }
}
