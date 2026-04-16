using Microsoft.Extensions.Options;

namespace PassReset.Web.Models;

/// <summary>
/// Validates <see cref="EmailNotificationSettings"/> at application startup.
/// When <see cref="EmailNotificationSettings.Enabled"/> is <c>true</c>, a subject
/// and body template are required — otherwise the feature silently sends empty
/// messages.
/// </summary>
public sealed class EmailNotificationSettingsValidator : IValidateOptions<EmailNotificationSettings>
{
    private static string Fmt(string path, string reason, string actual)
        => $"{path}: {reason} (got \"{actual}\"). Edit appsettings.Production.json or run Install-PassReset.ps1 -Reconfigure.";

    public ValidateOptionsResult Validate(string? name, EmailNotificationSettings options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Subject))
            failures.Add(Fmt(
                "EmailNotificationSettings.Subject",
                "must be non-empty when Enabled is true",
                ""));

        if (string.IsNullOrWhiteSpace(options.BodyTemplate))
            failures.Add(Fmt(
                "EmailNotificationSettings.BodyTemplate",
                "must be non-empty when Enabled is true",
                ""));

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
