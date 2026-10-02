using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AltMiUstMu.Web.Pages.Panel;

public sealed record TeamPickRow(TeamOverview Team, PickSide? Side, PickStatus? Status);

public sealed record HeadToHead(PunditView Pundit, int Agree, int Compared, int? Diff);

public sealed record ChartData(IReadOnlyList<string> Labels, IReadOnlyList<int?> Ranks, IReadOnlyList<int> Projected, int MaxRank);

public class IndexModel(SeasonService seasons, GameQueries queries) : PageModel
{
    public Season? Season { get; private set; }
    public bool Locked { get; private set; }
    public UserScore? Score { get; private set; }
    public int RankedCount { get; private set; }
    public int PickCount { get; private set; }
    public int TeamCount { get; private set; } = PickRules.TeamCount;
    public List<TeamPickRow> Rows { get; private set; } = [];
    public List<HeadToHead> HeadToHeads { get; private set; } = [];
    public ChartData? Chart { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return;
        }

        var userId = User.UserId()!;
        Locked = seasons.IsLocked(Season);
        var teams = await queries.GetTeamsAsync(Season, ct);
        var picks = await queries.GetPicksAsync(Season.Id, userId, ct);
        var pundits = await queries.GetPunditsAsync(Season.Id, ct);
        Score = await queries.GetScoreAsync(Season.Id, userId, ct);
        RankedCount = await queries.RankedCountAsync(Season.Id, ct);
        TeamCount = teams.Count;
        PickCount = picks.Count;

        Rows = teams
            .Select(t => picks.TryGetValue(t.Id, out var side)
                ? new TeamPickRow(t, side, t.StatusFor(side))
                : new TeamPickRow(t, null, null))
            .OrderBy(r => Order(r.Status))
            .ThenByDescending(r => r.Team.Projected is { } p ? Math.Abs(p - r.Team.Line) : 0)
            .ToList();

        HeadToHeads = pundits.Select(p =>
        {
            var compared = picks.Keys.Intersect(p.Picks.Keys).ToList();
            var agree = compared.Count(id => picks[id] == p.Picks[id]);
            int? diff = Score is not null && p.Score is not null ? Score.ProjectedCorrect - p.Score.ProjectedCorrect : null;
            return new HeadToHead(p, agree, compared.Count, diff);
        }).ToList();

        var history = await queries.GetHistoryAsync(Season.Id, userId, ct);
        if (history.Count > 0)
        {
            Chart = new ChartData(
                history.Select(h => Display.Date(h.Date)).ToList(),
                history.Select(h => h.Rank).ToList(),
                history.Select(h => h.ProjectedCorrect).ToList(),
                Math.Max(RankedCount, history.Max(h => h.Rank ?? 1)));
        }
    }

    private static int Order(PickStatus? status) => status switch
    {
        PickStatus.ClinchedWin => 0,
        PickStatus.TrendingWin => 1,
        PickStatus.Pending => 2,
        PickStatus.Push => 3,
        PickStatus.TrendingLoss => 4,
        PickStatus.ClinchedLoss => 5,
        _ => 6,
    };
}
