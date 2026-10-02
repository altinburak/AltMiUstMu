using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Identity;
using AltMiUstMu.Web.Infrastructure;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Liderlik;

[OutputCache(PolicyName = PublicPageCache.PolicyName)]
public class IndexModel(SeasonService seasons, GameQueries queries, AppDbContext db) : PageModel
{
    public const int PageSize = 25;

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public Season? Season { get; private set; }
    public bool Locked { get; private set; }
    public List<PlayerRow> Rows { get; private set; } = [];
    public int Total { get; private set; }
    public int RankedTotal { get; private set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public PlayerRow? Me { get; private set; }
    public int MyPickCount { get; private set; }
    public int CompletePlayers { get; private set; }
    public string? MyId { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return Page();
        }

        Locked = seasons.IsLocked(Season);
        MyId = User.UserId();
        PageNumber = Math.Max(1, PageNumber);

        var ranked = from s in db.UserScores.AsNoTracking()
                     join u in db.Users on s.UserId equals u.Id
                     where s.SeasonId == Season.Id && s.Rank != null
                     select new { s, u.DisplayName, u.DisplayNameKey, u.IsPundit, u.Id };

        RankedTotal = await ranked.CountAsync(ct);
        var filtered = ranked;
        var term = TextNormalizer.Fold(Query);
        if (term.Length > 0)
        {
            filtered = filtered.Where(x => x.DisplayNameKey.Contains(term));
        }

        Total = await filtered.CountAsync(ct);
        PageNumber = Math.Min(PageNumber, PageCount);
        Rows = (await filtered.OrderBy(x => x.s.Rank).ThenBy(x => x.DisplayNameKey)
                .Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync(ct))
            .Select(x => new PlayerRow(x.Id, x.DisplayName, x.IsPundit, x.s)).ToList();

        if (MyId is not null)
        {
            var mine = await queries.GetScoreAsync(Season.Id, MyId, ct);
            MyPickCount = mine?.PicksCount ?? await db.Picks.CountAsync(p => p.SeasonId == Season.Id && p.UserId == MyId, ct);
            if (mine?.Rank is not null)
            {
                Me = new PlayerRow(MyId, User.DisplayName(), false, mine);
            }
        }

        if (!Locked)
        {
            CompletePlayers = await queries.CompletePlayersAsync(Season.Id, PickRules.TeamCount, ct);
        }

        return Request.IsHtmx() ? Partial("_Board", this) : Page();
    }

    public string PageUrl(int page) =>
        Url.Page("/Liderlik/Index", new { q = string.IsNullOrWhiteSpace(Query) ? null : Query, p = page == 1 ? (int?)null : page })!;
}
