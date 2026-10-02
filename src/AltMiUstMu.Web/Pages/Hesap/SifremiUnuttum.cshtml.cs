using System.ComponentModel.DataAnnotations;
using System.Text;
using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace AltMiUstMu.Web.Pages.Hesap;

[EnableRateLimiting("auth")]
public class SifremiUnuttumModel(UserManager<AppUser> users, AccountEmails emails, AccountSettings settings, ILogger<SifremiUnuttumModel> logger) : PageModel
{
    public bool EmailEnabled => settings.RequireEmailConfirmation;

    [BindProperty]
    [Required(ErrorMessage = "E-posta zorunlu.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi gir.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    public bool Sent { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!EmailEnabled || !ModelState.IsValid)
        {
            return Page();
        }

        var user = await users.FindByEmailAsync(Email.Trim());
        if (user is { EmailConfirmed: true, IsPundit: false, IsDisabled: false })
        {
            try
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                await emails.SendPasswordResetAsync(user.Email!, $"/hesap/sifre-sifirla?email={Uri.EscapeDataString(user.Email!)}&code={code}", user.Language);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Password reset email failed for {UserId}", user.Id);
            }
        }

        Sent = true;
        return Page();
    }
}
