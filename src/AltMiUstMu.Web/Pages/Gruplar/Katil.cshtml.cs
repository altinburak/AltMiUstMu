using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Gruplar;

[EnableRateLimiting("writes")]
public class KatilModel(AppDbContext db, GroupService groups) : PageModel
{
    public string Code { get; private set; } = "";
    public string GroupName { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public string OwnerName { get; private set; } = "";
    public int MemberCount { get; private set; }
    public bool AlreadyMember { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(string code, CancellationToken ct) =>
        await LoadAsync(code, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(string code, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var result = await groups.JoinAsync(User.UserId()!, code, ct);
        if (!result.Success)
        {
            if (!await LoadAsync(code, ct))
            {
                return NotFound();
            }

            Error = result.Error?.ToString();
            return Page();
        }

        TempData["Toast"] = Lang.T($"{result.Group!.Name} grubuna hoş geldin!", $"Welcome to {result.Group!.Name}!");
        return Redirect($"/gruplar/{result.Group.Slug}");
    }

    private async Task<bool> LoadAsync(string code, CancellationToken ct)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        var group = await db.Groups.AsNoTracking()
            .Where(g => g.InviteCode == normalized)
            .Select(g => new { g.Name, g.Slug, g.OwnerId, Members = g.Members.Select(m => m.UserId).ToList() })
            .SingleOrDefaultAsync(ct);
        if (group is null)
        {
            return false;
        }

        Code = normalized;
        GroupName = group.Name;
        Slug = group.Slug;
        MemberCount = group.Members.Count;
        OwnerName = await db.Users.Where(u => u.Id == group.OwnerId).Select(u => u.DisplayName).SingleOrDefaultAsync(ct) ?? "";
        AlreadyMember = User.UserId() is { } me && group.Members.Contains(me);
        return true;
    }
}
