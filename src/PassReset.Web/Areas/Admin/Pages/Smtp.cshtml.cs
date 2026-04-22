using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PassReset.Web.Services.Configuration;

namespace PassReset.Web.Areas.Admin.Pages;

public sealed class SmtpModel : PageModel
{
    private readonly IAppSettingsEditor _editor;
    private readonly ISecretStore _secrets;

    public SmtpModel(IAppSettingsEditor editor, ISecretStore secrets)
    {
        _editor = editor;
        _secrets = secrets;
    }

    [BindProperty] public SmtpInput Input { get; set; } = new();

    public void OnGet()
    {
        var snap = _editor.Load();
        Input = new SmtpInput
        {
            Host = snap.Smtp.Host,
            Port = snap.Smtp.Port,
            Username = snap.Smtp.Username,
            FromAddress = snap.Smtp.FromAddress,
            UseStartTls = snap.Smtp.UseStartTls,
        };
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();

        var snap = _editor.Load();
        _editor.Save(snap with
        {
            Smtp = new Services.Configuration.SmtpSection(
                Host: Input.Host ?? "",
                Port: Input.Port,
                Username: Input.Username ?? "",
                FromAddress: Input.FromAddress ?? "",
                UseStartTls: Input.UseStartTls)
        });

        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            var bundle = _secrets.Load();
            _secrets.Save(bundle with { SmtpPassword = Input.NewPassword });
        }

        TempData["Success"] = "SMTP settings saved. Recycle the app pool to apply.";
        return RedirectToPage();
    }

    public sealed class SmtpInput
    {
        [Required, StringLength(255)] public string? Host { get; set; }
        [Range(1, 65535)] public int Port { get; set; } = 25;
        [StringLength(255)] public string? Username { get; set; }
        [StringLength(255)] public string? NewPassword { get; set; }
        [Required, EmailAddress, StringLength(255)] public string? FromAddress { get; set; }
        public bool UseStartTls { get; set; } = true;
    }
}
