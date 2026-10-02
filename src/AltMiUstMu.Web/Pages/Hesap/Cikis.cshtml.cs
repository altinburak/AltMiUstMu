using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Web.Localization;
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
        TempData["Toast"] = Lang.T("Çıkış yaptın. Görüşmek üzere!", "You signed out. See you soon!");
        return Redirect("/");
    }
}
