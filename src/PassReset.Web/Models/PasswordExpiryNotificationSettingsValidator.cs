using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace PassReset.Web.Models;

/// <summary>
/// Validates <see cref="PasswordExpiryNotificationSettings"/> at application startup.
/// Only runs when the background service is enabled.
/// </summary>
public sealed class PasswordExpiryNotificationSettingsValidator
    : IValidateOptions<PasswordExpiryNotificationSettings>
{
    private static readonly Regex TimePattern = new(@"^\d{2}:\d{2}$", RegexOptions.Compiled);

    private static string Fmt(string path, string reason, string actual)
        => $"{path}: {reason} (got \"{actual}\"). Edit appsettings.Production.json or run Install-PassReset.ps1 -Reconfigure.";

    public ValidateOptionsResult Validate(string? name, PasswordExpiryNotificationSettings options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (options.DaysBeforeExpiry < 1)
            failures.Add(Fmt(
                "PasswordExpiryNotificationSettings.DaysBeforeExpiry",
                "must be >= 1 when Enabled is true",
                options.DaysBeforeExpiry.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        if (string.IsNullOrWhiteSpace(options.NotificationTimeUtc)
            || !TimePattern.IsMatch(options.NotificationTimeUtc))
        {
            failures.Add(Fmt(
                "PasswordExpiryNotificationSettings.NotificationTimeUtc",
                "must match 'HH:mm' format (24-hour)",
                options.NotificationTimeUtc ?? ""));
        }

        if (string.IsNullOrWhiteSpace(options.PassResetUrl))
        {
            failures.Add(Fmt(
                "PasswordExpiryNotificationSettings.PassResetUrl",
                "must be non-empty when Enabled is true",
                ""));
        }
        else if (!options.PassResetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(Fmt(
                "PasswordExpiryNotificationSettings.PassResetUrl",
                "must start with 'https://'",
                options.PassResetUrl));
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
