using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Gruplar;

public sealed record GroupListItem(string Name, string Slug, int Members, bool IsOwner, int? MyGroupRank);

[EnableRateLimiting("writes")]
public class IndexModel(AppDbContext db, GroupService groups) : PageModel
{
    public List<GroupListItem> Groups { get; private set; } = [];

    [BindProperty]
    public string? NewName { get; set; }

    [BindProperty]
    public string? InviteCode { get; set; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var result = await groups.CreateAsync(User.UserId()!, NewName ?? "", ct);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(NewName), result.Error!.ToString());
            await LoadAsync(ct);
            return Page();
        }

        TempData["Toast"] = Lang.T("Grup kuruldu! Davet bağlantısını arkadaşlarınla paylaş.", "Group created! Share the invite link with your friends.");
        return Redirect($"/gruplar/{result.Group!.Slug}");
    }

    public async Task<IActionResult> OnPostJoinAsync(CancellationToken ct)
    {
        // Accept either a bare code or a full invite link.
        var code = (InviteCode ?? "").Trim().TrimEnd('/');
        var slash = code.LastIndexOf('/');
        if (slash >= 0)
        {
            code = code[(slash + 1)..];
        }

        var result = await groups.JoinAsync(User.UserId()!, code, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(InviteCode), result.Error!.ToString());
            await LoadAsync(ct);
            return Page();
        }

        TempData["Toast"] = Lang.T($"{result.Group!.Name} grubuna katıldın.", $"You joined {result.Group!.Name}.");
        return Redirect($"/gruplar/{result.Group.Slug}");
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var userId = User.UserId()!;
        var mine = await db.Groups.AsNoTracking()
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .OrderBy(g => g.Name)
            .Select(g => new { g.Id, g.Name, g.Slug, g.OwnerId, g.SeasonId, Members = g.Members.Select(m => m.UserId).ToList() })
            .ToListAsync(ct);

        var seasonIds = mine.Select(g => g.SeasonId).Distinct().ToList();
        var allMemberIds = mine.SelectMany(g => g.Members).Distinct().ToList();
        var ranks = await db.UserScores.AsNoTracking()
            .Where(s => seasonIds.Contains(s.SeasonId) && allMemberIds.Contains(s.UserId) && s.Rank != null)
            .Select(s => new { s.SeasonId, s.UserId, s.Rank })
            .ToListAsync(ct);

        Groups = mine.Select(g =>
        {
            var ordered = ranks.Where(r => r.SeasonId == g.SeasonId && g.Members.Contains(r.UserId)).OrderBy(r => r.Rank).ToList();
            var index = ordered.FindIndex(r => r.UserId == userId);
            return new GroupListItem(g.Name, g.Slug, g.Members.Count, g.OwnerId == userId, index >= 0 ? index + 1 : null);
        }).ToList();
    }
}
