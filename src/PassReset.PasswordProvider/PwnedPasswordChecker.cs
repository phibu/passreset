using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PassReset.PasswordProvider;

/// <summary>
/// Checks a password against the HaveIBeenPwned k-anonymity API.
/// Instance-based class wired through <see cref="IHttpClientFactory"/> so the underlying
/// <see cref="HttpMessageHandler"/> can be substituted in tests.
/// See https://haveibeenpwned.com/API/v2#PwnedPasswords
/// </summary>
public sealed class PwnedPasswordChecker
{
    private readonly HttpClient _http;
    private readonly ILogger<PwnedPasswordChecker>? _logger;

    /// <summary>
    /// Creates a new checker using the injected <see cref="HttpClient"/>.
    /// Callers must configure a reasonable BaseAddress / Timeout on the underlying client.
    /// </summary>
    public PwnedPasswordChecker(HttpClient http, ILogger<PwnedPasswordChecker>? logger = null)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether the password appears in the HaveIBeenPwned database.
    /// Returns <see langword="true"/> if confirmed pwned, <see langword="false"/> if confirmed clean,
    /// or <see langword="null"/> if the API was unreachable so the caller can surface a distinct error.
    /// </summary>
    public async Task<bool?> IsPwnedPasswordAsync(string plaintext)
    {
        try
        {
            var hash = ComputeSha1Hex(plaintext).ToUpperInvariant();
            var prefix = hash[..5];
            var suffix = hash[5..];

            using var response = await _http.GetAsync(
                $"range/{prefix}").ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("HaveIBeenPwned API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var colon = line.IndexOf(':');
                if (colon > 0 && line[..colon].Trim().Equals(suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            // API unreachable — return null so the caller can surface a distinct error
            // rather than silently blocking the password change.
            _logger?.LogWarning(ex, "HaveIBeenPwned API call failed");
            return null;
        }
    }

    private static string ComputeSha1Hex(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
