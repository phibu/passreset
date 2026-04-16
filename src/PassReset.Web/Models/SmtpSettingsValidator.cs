using Microsoft.Extensions.Options;

namespace PassReset.Web.Models;

/// <summary>
/// Validates <see cref="SmtpSettings"/> at application startup. SMTP is considered
/// configured when <see cref="SmtpSettings.Host"/> is non-empty; additional fields
/// are then required. When <see cref="SmtpSettings.Host"/> is empty the relay is
/// disabled and validation succeeds silently.
/// </summary>
public sealed class SmtpSettingsValidator : IValidateOptions<SmtpSettings>
{
    private static string Fmt(string path, string reason, string actual)
        => $"{path}: {reason} (got \"{actual}\"). Edit appsettings.Production.json or run Install-PassReset.ps1 -Reconfigure.";

    public ValidateOptionsResult Validate(string? name, SmtpSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (options.Port <= 0 || options.Port > 65535)
            failures.Add(Fmt(
                "SmtpSettings.Port",
                "must be a valid TCP port (1-65535)",
                options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        if (string.IsNullOrWhiteSpace(options.FromAddress) || !options.FromAddress.Contains('@'))
            failures.Add(Fmt(
                "SmtpSettings.FromAddress",
                "must be a valid email address (must contain '@')",
                options.FromAddress ?? ""));

        var hasUser = !string.IsNullOrEmpty(options.Username);
        var hasPass = !string.IsNullOrEmpty(options.Password);
        if (hasUser != hasPass)
        {
            if (hasUser && !hasPass)
                failures.Add(Fmt(
                    "SmtpSettings.Password",
                    "must be non-empty when Username is set (use empty Username+Password for anonymous relay)",
                    "<redacted>"));
            else
                failures.Add(Fmt(
                    "SmtpSettings.Username",
                    "must be non-empty when Password is set (use empty Username+Password for anonymous relay)",
                    options.Username ?? ""));
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
