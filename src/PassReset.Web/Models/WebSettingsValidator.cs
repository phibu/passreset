using Microsoft.Extensions.Options;

namespace PassReset.Web.Models;

/// <summary>
/// Validates <see cref="WebSettings"/> at application startup.
/// Cross-field rule <c>UseDebugProvider =&gt; IsDevelopment</c> requires <see cref="IHostEnvironment"/>
/// which <see cref="IValidateOptions{T}"/> cannot access; that check remains inline in
/// <c>Program.cs</c>. This validator currently has no type-only invariants to assert and
/// always succeeds, but the hook is in place so future type-only rules can be added here.
/// </summary>
public sealed class WebSettingsValidator : IValidateOptions<WebSettings>
{
    public ValidateOptionsResult Validate(string? name, WebSettings options)
        => ValidateOptionsResult.Success;
}
