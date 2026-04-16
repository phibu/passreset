using Microsoft.Extensions.Options;

namespace PassReset.PasswordProvider;

/// <summary>
/// Validates <see cref="PasswordChangeOptions"/> at application startup so that
/// mis-configuration is caught immediately with a clear error rather than producing
/// a cryptic LDAP socket error at runtime.
/// </summary>
public sealed class PasswordChangeOptionsValidator : IValidateOptions<PasswordChangeOptions>
{
    private static string Fmt(string path, string reason, string actual)
        => $"{path}: {reason} (got \"{actual}\"). Edit appsettings.Production.json or run Install-PassReset.ps1 -Reconfigure.";

    public ValidateOptionsResult Validate(string? name, PasswordChangeOptions options)
    {
        if (options.UseAutomaticContext)
            return ValidateOptionsResult.Success;

        // Manual context requires at least one resolvable hostname.
        if (options.LdapHostnames.Length == 0
            || options.LdapHostnames.All(h => string.IsNullOrWhiteSpace(h)))
        {
            return ValidateOptionsResult.Fail(Fmt(
                "PasswordChangeOptions.LdapHostnames",
                "must contain at least one non-empty hostname when UseAutomaticContext is false",
                "[]"));
        }

        if (options.LdapPort <= 0 || options.LdapPort > 65535)
        {
            return ValidateOptionsResult.Fail(Fmt(
                "PasswordChangeOptions.LdapPort",
                "is not a valid port number (use 636 for LDAPS, 389 for plain LDAP)",
                options.LdapPort.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return ValidateOptionsResult.Success;
    }
}
