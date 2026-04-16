using Microsoft.Extensions.Options;

namespace PassReset.Web.Models;

/// <summary>
/// Validates <see cref="ClientSettings"/> at application startup so mis-configuration
/// produces a clear, operator-actionable error at DI build rather than a cryptic
/// runtime failure (e.g. silent reCAPTCHA rejection).
/// </summary>
public sealed class ClientSettingsValidator : IValidateOptions<ClientSettings>
{
    private static string Fmt(string path, string reason, string actual)
        => $"{path}: {reason} (got \"{actual}\"). Edit appsettings.Production.json or run Install-PassReset.ps1 -Reconfigure.";

    public ValidateOptionsResult Validate(string? name, ClientSettings options)
    {
        var r = options.Recaptcha;
        if (r is null || !r.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(r.SiteKey))
            failures.Add(Fmt(
                "ClientSettings.Recaptcha.SiteKey",
                "must be non-empty when Recaptcha.Enabled is true",
                ""));

        if (string.IsNullOrWhiteSpace(r.PrivateKey))
            failures.Add(Fmt(
                "ClientSettings.Recaptcha.PrivateKey",
                "must be non-empty when Recaptcha.Enabled is true",
                "<redacted>"));

        if (r.ScoreThreshold < 0.0f || r.ScoreThreshold > 1.0f)
            failures.Add(Fmt(
                "ClientSettings.Recaptcha.ScoreThreshold",
                "must be between 0.0 and 1.0 inclusive",
                r.ScoreThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
