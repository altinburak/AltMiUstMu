using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AltMiUstMu.Web.Pages.Hesap;

public class CikisModel(SignInManager<AppUser> signIn) : PageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        await signIn.SignOutAsync();
        TempData["Toast"] = "Çıkış yaptın. Görüşmek üzere!";
        return Redirect("/");
    }
}
