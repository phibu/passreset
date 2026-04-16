using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace PassReset.Tests.Web.Startup;

/// <summary>
/// End-to-end validation that <see cref="ValidateOnStart"/> wiring fails fast when
/// appsettings are invalid. Mirrors the existing <c>DebugFactory</c> pattern:
/// each test class owns a dedicated <see cref="WebApplicationFactory{TEntryPoint}"/>
/// subclass with <see cref="IWebHostBuilder.ConfigureAppConfiguration"/> overrides.
///
/// Why subclasses instead of inline <c>new WebApplicationFactory&lt;Program&gt;()</c>?
/// <c>HostFactoryResolver.HostingListener</c> uses a process-wide DiagnosticListener
/// to intercept <c>builder.Build()</c> inside top-level <c>Program.cs</c>. Multiple
/// inline factories can race with existing <c>DebugFactory</c> tests in the same
/// process and miss the intercept, producing "entry point did not build an IHost".
/// Subclassing with a dedicated <see cref="ConfigureWebHost"/> override keeps the
/// listener state deterministic.
/// </summary>
public class StartupValidationTests
{
    /// <summary>
    /// Factory for the invalid-PasswordChangeOptions scenario (empty LdapHostnames + out-of-range port).
    /// </summary>
    public sealed class InvalidPasswordChangeOptionsFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WebSettings:UseDebugProvider"] = "true",
                    ["WebSettings:EnableHttpsRedirect"] = "false",
                    ["ClientSettings:Recaptcha:Enabled"] = "false",
                    ["SmtpSettings:Host"] = "",
                    ["SiemSettings:Syslog:Enabled"] = "false",
                    ["SiemSettings:AlertEmail:Enabled"] = "false",
                    ["EmailNotificationSettings:Enabled"] = "false",
                    ["PasswordExpiryNotificationSettings:Enabled"] = "false",
                    // Invalid combo — validator must trip at ValidateOnStart.
                    ["PasswordChangeOptions:UseAutomaticContext"] = "false",
                    ["PasswordChangeOptions:LdapHostnames:0"] = "",
                    ["PasswordChangeOptions:LdapPort"] = "99999",
                });
            });
        }
    }

    [Fact]
    public void Build_WithInvalidPasswordChangeOptions_ThrowsOptionsValidationException()
    {
        using var factory = new InvalidPasswordChangeOptionsFactory();

        var ex = Assert.ThrowsAny<Exception>(() =>
        {
            // Force the host to build — WebApplicationFactory defers until first client request.
            using var client = factory.CreateClient();
        });

        var chain = Flatten(ex).ToList();
        // Either the OptionsValidationException is in the chain, or its D-08 message content
        // survives in the flattened exception messages (WebApplicationFactory can re-wrap).
        Assert.True(
            chain.Any(e => e is OptionsValidationException)
            || chain.Any(e => (e.Message ?? string.Empty).Contains("PasswordChangeOptions.LdapHostnames")
                              || (e.Message ?? string.Empty).Contains("PasswordChangeOptions.LdapPort")),
            "Expected OptionsValidationException or D-08 message in exception chain. Got: "
                + string.Join(" | ", chain.Select(e => $"{e.GetType().Name}: {e.Message}")));
    }

    private static IEnumerable<Exception> Flatten(Exception ex)
    {
        var current = ex;
        while (current is not null)
        {
            yield return current;
            if (current is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                    foreach (var x in Flatten(inner))
                        yield return x;
            }
            current = current.InnerException;
        }
    }
}
