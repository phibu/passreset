using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PassReset.Common;
using PassReset.Web.Services.Configuration;

namespace PassReset.Web.Areas.Admin.Pages;

public sealed class LdapModel : PageModel
{
    private readonly IAppSettingsEditor _editor;
    private readonly ISecretStore _secrets;

    public LdapModel(IAppSettingsEditor editor, ISecretStore secrets)
    {
        _editor = editor;
        _secrets = secrets;
    }

    [BindProperty] public LdapInput Input { get; set; } = new();

    public void OnGet()
    {
        var snap = _editor.Load();
        Input = new LdapInput
        {
            UseAutomaticContext = snap.PasswordChange.UseAutomaticContext,
            ProviderMode = snap.PasswordChange.ProviderMode,
            LdapHostnamesCsv = string.Join(", ", snap.PasswordChange.LdapHostnames),
            LdapPort = snap.PasswordChange.LdapPort,
            LdapUseSsl = snap.PasswordChange.LdapUseSsl,
            BaseDn = snap.PasswordChange.BaseDn,
            ServiceAccountDn = snap.PasswordChange.ServiceAccountDn,
            DefaultDomain = snap.PasswordChange.DefaultDomain,
        };
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();

        var snap = _editor.Load();
        var hostnames = (Input.LdapHostnamesCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _editor.Save(snap with
        {
            PasswordChange = snap.PasswordChange with
            {
                UseAutomaticContext = Input.UseAutomaticContext,
                ProviderMode = Input.ProviderMode,
                LdapHostnames = hostnames,
                LdapPort = Input.LdapPort,
                LdapUseSsl = Input.LdapUseSsl,
                BaseDn = Input.BaseDn ?? "",
                ServiceAccountDn = Input.ServiceAccountDn ?? "",
                DefaultDomain = Input.DefaultDomain ?? "",
            }
        });

        if (!string.IsNullOrEmpty(Input.NewLdapPassword) || !string.IsNullOrEmpty(Input.NewServiceAccountPassword))
        {
            var bundle = _secrets.Load();
            _secrets.Save(bundle with
            {
                LdapPassword = string.IsNullOrEmpty(Input.NewLdapPassword) ? bundle.LdapPassword : Input.NewLdapPassword,
                ServiceAccountPassword = string.IsNullOrEmpty(Input.NewServiceAccountPassword) ? bundle.ServiceAccountPassword : Input.NewServiceAccountPassword,
            });
        }

        TempData["Success"] = "LDAP settings saved. Recycle the app pool to apply.";
        return RedirectToPage();
    }

    public sealed class LdapInput
    {
        public bool UseAutomaticContext { get; set; }
        public ProviderMode ProviderMode { get; set; } = ProviderMode.Auto;
        [StringLength(1024)] public string? LdapHostnamesCsv { get; set; }
        [Range(1, 65535)] public int LdapPort { get; set; } = 636;
        public bool LdapUseSsl { get; set; } = true;
        [StringLength(512)] public string? BaseDn { get; set; }
        [StringLength(512)] public string? ServiceAccountDn { get; set; }
        [StringLength(255)] public string? DefaultDomain { get; set; }
        [StringLength(255)] public string? NewLdapPassword { get; set; }
        [StringLength(255)] public string? NewServiceAccountPassword { get; set; }
    }
}
