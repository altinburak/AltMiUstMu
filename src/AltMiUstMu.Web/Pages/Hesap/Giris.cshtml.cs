using System.ComponentModel.DataAnnotations;
using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AltMiUstMu.Web.Pages.Hesap;

[EnableRateLimiting("auth")]
public class GirisModel(SignInManager<AppUser> signIn, UserManager<AppUser> users, ILogger<GirisModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool ShowResendLink { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "E-posta zorunlu.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi gir.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Şifre zorunlu.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; } = "";

        [Display(Name = "Beni hatırla")]
        public bool RememberMe { get; set; } = true;
    }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signIn.PasswordSignInAsync(Input.Email.Trim(), Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            logger.LogInformation("User signed in");
            return LocalRedirect(SafeReturnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Çok fazla hatalı deneme yaptın. Hesabın 15 dakikalığına kilitlendi.");
            return Page();
        }

        if (result.IsNotAllowed)
        {
            var user = await users.FindByEmailAsync(Input.Email.Trim());
            if (user is not null && await users.CheckPasswordAsync(user, Input.Password))
            {
                if (user.IsDisabled || user.IsPundit)
                {
                    ModelState.AddModelError(string.Empty, "Bu hesapla giriş yapılamıyor.");
                    return Page();
                }

                if (!user.EmailConfirmed)
                {
                    ShowResendLink = true;
                    ModelState.AddModelError(string.Empty, "E-posta adresini henüz doğrulamadın. Gelen kutunu (ve spam klasörünü) kontrol et.");
                    return Page();
                }
            }
        }

        ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
        return Page();
    }

    private string SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/tahminler";
}
