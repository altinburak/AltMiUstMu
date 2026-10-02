using System.ComponentModel.DataAnnotations;
using System.Text;
using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Web.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace AltMiUstMu.Web.Pages.Hesap;

[EnableRateLimiting("auth")]
public class SifreSifirlaModel(UserManager<AppUser> users) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Done { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "E-posta zorunlu.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi gir.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Şifre zorunlu.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı.")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifre")]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
        [Display(Name = "Yeni şifre (tekrar)")]
        public string ConfirmPassword { get; set; } = "";

        [Required]
        public string Code { get; set; } = "";
    }

    public IActionResult OnGet(string? code, string? email)
    {
        if (string.IsNullOrEmpty(code))
        {
            return Redirect("/hesap/sifremi-unuttum");
        }

        Input = new InputModel { Code = code, Email = email ?? "" };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await users.FindByEmailAsync(Input.Email.Trim());
        if (user is null || user.IsPundit)
        {
            // Do not reveal that the user does not exist.
            Done = true;
            return Page();
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Code));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, Lang.T("Bağlantı geçersiz veya süresi dolmuş.", "This link is invalid or has expired."));
            return Page();
        }

        var result = await users.ResetPasswordAsync(user, token, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        Done = true;
        return Page();
    }
}
