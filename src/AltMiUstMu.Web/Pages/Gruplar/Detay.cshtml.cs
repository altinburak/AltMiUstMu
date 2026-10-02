using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Gruplar;

public sealed record GroupRow(string UserId, string DisplayName, bool IsPundit, bool IsOwner, UserScore? Score, int PickCount, int? GroupRank);

[EnableRateLimiting("writes")]
public class DetayModel(AppDbContext db, SeasonService seasons, GroupService groups, AccountEmails links) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "mutfak")]
    public bool IncludePundits { get; set; }

    public Group Group { get; private set; } = null!;
    public Season Season { get; private set; } = null!;
    public bool Locked { get; private set; }
    public bool IsMember { get; private set; }
    public bool IsOwner { get; private set; }
    public string? MyId { get; private set; }
    public List<GroupRow> Rows { get; private set; } = [];
    public string InviteUrl => links.Absolute($"/gruplar/katil/{Group.InviteCode}");
    public string BoardUrl => $"/gruplar/{Group.Slug}?handler=Board";

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct) =>
        await LoadAsync(slug, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnGetBoardAsync(string slug, CancellationToken ct) =>
        await LoadAsync(slug, ct) ? Partial("_GroupBoard", this) : NotFound();

    public async Task<IActionResult> OnPostRemoveAsync(string slug, string memberId, CancellationToken ct)
    {
        if (!await LoadAsync(slug, ct))
        {
            return NotFound();
        }

        var result = await groups.RemoveMemberAsync(MyId ?? "", Group.Id, memberId, ct);
        if (!result.Success)
        {
            Response.Headers["HX-Trigger"] = System.Text.Json.JsonSerializer.Serialize(new { toast = new { message = result.Error?.ToString(), type = "error" } });
        }
        else
        {
            Response.Headers["HX-Trigger"] = System.Text.Json.JsonSerializer.Serialize(new { toast = new { message = Lang.T("Üye gruptan çıkarıldı.", "Member removed from the group.") } });
        }

        await LoadAsync(slug, ct);
        return Partial("_GroupBoard", this);
    }

    public async Task<IActionResult> OnPostLeaveAsync(string slug, CancellationToken ct)
    {
        if (!await LoadAsync(slug, ct))
        {
            return NotFound();
        }

        var result = await groups.LeaveAsync(MyId ?? "", Group.Id, ct);
        TempData[result.Success ? "Toast" : "ToastError"] = result.Success ? Lang.T($"{Group.Name} grubundan ayrıldın.", $"You left {Group.Name}.") : result.Error?.ToString();
        return RedirectOrHx("/gruplar");
    }

    public async Task<IActionResult> OnPostDeleteAsync(string slug, CancellationToken ct)
    {
        if (!await LoadAsync(slug, ct))
        {
            return NotFound();
        }

        var result = await groups.DeleteAsync(MyId ?? "", Group.Id, ct);
        TempData[result.Success ? "Toast" : "ToastError"] = result.Success ? Lang.T($"{Group.Name} silindi.", $"{Group.Name} was deleted.") : result.Error?.ToString();
        return RedirectOrHx("/gruplar");
    }

    private IActionResult RedirectOrHx(string url)
    {
        if (Request.IsHtmx())
        {
            Response.Headers["HX-Redirect"] = url;
            return new NoContentResult();
        }

        return Redirect(url);
    }

    private async Task<bool> LoadAsync(string slug, CancellationToken ct)
    {
        var group = await db.Groups.AsNoTracking().Include(g => g.Members).SingleOrDefaultAsync(g => g.Slug == slug, ct);
        var season = group is null ? null : await db.Seasons.AsNoTracking().SingleOrDefaultAsync(s => s.Id == group.SeasonId, ct);
        if (group is null || season is null)
        {
            return false;
        }

        Group = group;
        Season = season;
        Locked = seasons.IsLocked(season);
        MyId = User.UserId();
        IsMember = MyId is not null && group.Members.Any(m => m.UserId == MyId);
        IsOwner = MyId == group.OwnerId;

        var memberIds = group.Members.Select(m => m.UserId).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => (memberIds.Contains(u.Id) || (IncludePundits && u.IsPundit)) && !u.IsDisabled)
            .Select(u => new { u.Id, u.DisplayName, u.IsPundit })
            .ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToList();
        var scores = await db.UserScores.AsNoTracking().Where(s => s.SeasonId == season.Id && ids.Contains(s.UserId)).ToDictionaryAsync(s => s.UserId, ct);
        var pickCounts = await db.Picks.AsNoTracking().Where(p => p.SeasonId == season.Id && ids.Contains(p.UserId))
            .GroupBy(p => p.UserId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var rows = users.Select(u => new
        {
            u.Id,
            u.DisplayName,
            u.IsPundit,
            Score = scores.GetValueOrDefault(u.Id),
            Picks = pickCounts.GetValueOrDefault(u.Id),
        }).ToList();

        if (Locked)
        {
            // Group rank follows the global ranking; equal global ranks share a group rank.
            var ranked = rows.Where(r => r.Score?.Rank is not null).OrderBy(r => r.Score!.Rank).ThenBy(r => r.DisplayName).ToList();
            var result = new List<GroupRow>();
            for (var i = 0; i < ranked.Count; i++)
            {
                var r = ranked[i];
                var groupRank = i > 0 && ranked[i - 1].Score!.Rank == r.Score!.Rank ? result[i - 1].GroupRank : i + 1;
                result.Add(new GroupRow(r.Id, r.DisplayName, r.IsPundit, r.Id == group.OwnerId, r.Score, r.Picks, groupRank));
            }

            result.AddRange(rows.Where(r => r.Score?.Rank is null).OrderBy(r => r.DisplayName)
                .Select(r => new GroupRow(r.Id, r.DisplayName, r.IsPundit, r.Id == group.OwnerId, r.Score, r.Picks, null)));
            Rows = result;
        }
        else
        {
            Rows = rows.OrderByDescending(r => r.Picks).ThenBy(r => r.DisplayName)
                .Select(r => new GroupRow(r.Id, r.DisplayName, r.IsPundit, r.Id == group.OwnerId, r.Score, r.Picks, null)).ToList();
        }

        return true;
    }
}
