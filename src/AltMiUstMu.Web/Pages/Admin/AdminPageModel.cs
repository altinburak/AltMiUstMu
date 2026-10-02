using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AltMiUstMu.Web.Pages.Admin;

/// <summary>Base for admin pages (the /Admin folder is restricted to the Admin policy in Program.cs).</summary>
public abstract class AdminPageModel : PageModel
{
    protected AuditActor Actor => new(User.UserId(), User.DisplayName());

    protected void Flash(string message) => TempData["Toast"] = message;

    protected void FlashError(string message) => TempData["ToastError"] = message;
}
