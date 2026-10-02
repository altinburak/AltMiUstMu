using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Infrastructure;
using AltMiUstMu.Web.Pages.Panel;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages;

[OutputCache(PolicyName = PublicPageCache.PolicyName)]
public class OyuncuModel(SeasonService seasons, GameQueries queries, AppDbContext db) : PageModel
{
    public Season Season { get; private set; } = null!;
    public string DisplayName { get; private set; } = "";
    public bool IsPundit { get; private set; }
    public bool IsMe { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public UserScore? Score { get; private set; }
    public int RankedCount { get; private set; }
    public int PickCount { get; private set; }
    public bool PicksVisible { get; private set; }
    public bool Locked { get; private set; }
    public List<TeamPickRow> Rows { get; private set; } = [];
    public ChartData? Chart { get; private set; }

    public async Task<IActionResult> OnGetAsync(string name, CancellationToken ct)
    {
        var season = await seasons.GetCurrentAsync(ct);
        var key = TextNormalizer.Fold(name);
        var user = await db.Users.AsNoTracking().Where(u => u.DisplayNameKey == key && !u.IsDisabled)
            .Select(u => new { u.Id, u.DisplayName, u.IsPundit, u.CreatedAt }).SingleOrDefaultAsync(ct);
        if (season is null || user is null)
        {
            return NotFound();
        }

        Season = season;
        DisplayName = user.DisplayName;
        IsPundit = user.IsPundit;
        JoinedAt = user.CreatedAt;
        IsMe = User.UserId() == user.Id;
        Locked = seasons.IsLocked(season);
        Score = await queries.GetScoreAsync(season.Id, user.Id, ct);
        RankedCount = await queries.RankedCountAsync(season.Id, ct);
        PicksVisible = PickRules.CanViewPicks(season, seasons.UtcNow, user.Id, user.IsPundit, User.UserId(), User.IsInRole("Admin"));

        var picks = await queries.GetPicksAsync(season.Id, user.Id, ct);
        PickCount = picks.Count;
        if (PicksVisible)
        {
            var teams = await queries.GetTeamsAsync(season, ct);
            Rows = teams.Select(t => picks.TryGetValue(t.Id, out var side)
                    ? new TeamPickRow(t, side, t.StatusFor(side))
                    : new TeamPickRow(t, null, null))
                .OrderBy(r => r.Team.Conference).ThenBy(r => r.Team.City).ToList();
        }

        var history = await queries.GetHistoryAsync(season.Id, user.Id, ct);
        if (history.Count > 0)
        {
            Chart = new ChartData(
                history.Select(h => Display.Date(h.Date)).ToList(),
                history.Select(h => h.Rank).ToList(),
                history.Select(h => h.ProjectedCorrect).ToList(),
                Math.Max(RankedCount, history.Max(h => h.Rank ?? 1)));
        }

        return Page();
    }
}
