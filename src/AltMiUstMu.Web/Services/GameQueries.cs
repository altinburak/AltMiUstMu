using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Services;

public sealed record TeamOverview(
    int Id,
    string Abbreviation,
    string City,
    string Name,
    Conference Conference,
    string Division,
    string PrimaryColor,
    string SecondaryColor,
    decimal Line,
    int Wins,
    int Losses,
    int GamesPerTeam,
    RecordSource Source,
    DateTime? RecordUpdatedAt,
    int OverCount,
    int UnderCount)
{
    public string FullName => $"{City} {Name}";
    public int GamesPlayed => Wins + Losses;
    public decimal? Projected => ScoringEngine.ProjectedWins(Wins, Losses, GamesPerTeam);
    public TeamState State => new(Line, Wins, Losses, GamesPerTeam);
    public int TotalPicks => OverCount + UnderCount;
    public double OverShare => TotalPicks == 0 ? 0 : (double)OverCount / TotalPicks;

    public PickStatus StatusFor(PickSide side) => ScoringEngine.Evaluate(side, State);
}

public sealed record PunditView(string UserId, string DisplayName, IReadOnlyDictionary<int, PickSide> Picks, UserScore? Score)
{
    public string ShortName => DisplayName.Split(' ')[0];
}

public sealed record PlayerRow(string UserId, string DisplayName, bool IsPundit, UserScore Score);

/// <summary>Read-side queries shared by several pages. Everything reads precomputed data; nothing is scored per request except per-team statuses.</summary>
public class GameQueries(AppDbContext db)
{
    public async Task<List<TeamOverview>> GetTeamsAsync(Season season, CancellationToken ct = default)
    {
        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.Conference).ThenBy(t => t.City).ToListAsync(ct);
        var lines = await db.TeamLines.AsNoTracking().Where(l => l.SeasonId == season.Id).ToDictionaryAsync(l => l.TeamId, l => l.Line, ct);
        var records = await db.TeamRecords.AsNoTracking().Where(r => r.SeasonId == season.Id).ToDictionaryAsync(r => r.TeamId, ct);

        // Community split counts regular (non-pundit, enabled) players only.
        var splits = await (
                from p in db.Picks.AsNoTracking()
                join u in db.Users on p.UserId equals u.Id
                where p.SeasonId == season.Id && !u.IsPundit && !u.IsDisabled
                group p by new { p.TeamId, p.Side } into g
                select new { g.Key.TeamId, g.Key.Side, Count = g.Count() })
            .ToListAsync(ct);

        return teams.Where(t => lines.ContainsKey(t.Id)).Select(t =>
        {
            records.TryGetValue(t.Id, out var r);
            return new TeamOverview(
                t.Id, t.Abbreviation, t.City, t.Name, t.Conference, t.Division, t.PrimaryColor, t.SecondaryColor,
                lines[t.Id], r?.Wins ?? 0, r?.Losses ?? 0, season.GamesPerTeam, r?.Source ?? RecordSource.Api, r?.UpdatedAt,
                splits.Where(s => s.TeamId == t.Id && s.Side == PickSide.Over).Sum(s => s.Count),
                splits.Where(s => s.TeamId == t.Id && s.Side == PickSide.Under).Sum(s => s.Count));
        }).ToList();
    }

    public async Task<Dictionary<int, PickSide>> GetPicksAsync(int seasonId, string userId, CancellationToken ct = default) =>
        await db.Picks.AsNoTracking().Where(p => p.SeasonId == seasonId && p.UserId == userId).ToDictionaryAsync(p => p.TeamId, p => p.Side, ct);

    public async Task<List<PunditView>> GetPunditsAsync(int seasonId, CancellationToken ct = default)
    {
        var pundits = await db.Users.AsNoTracking().Where(u => u.IsPundit && !u.IsDisabled).OrderBy(u => u.CreatedAt).ThenBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName }).ToListAsync(ct);
        var ids = pundits.Select(p => p.Id).ToList();
        var picks = await db.Picks.AsNoTracking().Where(p => p.SeasonId == seasonId && ids.Contains(p.UserId)).ToListAsync(ct);
        var scores = await db.UserScores.AsNoTracking().Where(s => s.SeasonId == seasonId && ids.Contains(s.UserId)).ToDictionaryAsync(s => s.UserId, ct);

        return pundits.Select(p => new PunditView(
            p.Id,
            p.DisplayName,
            picks.Where(x => x.UserId == p.Id).ToDictionary(x => x.TeamId, x => x.Side),
            scores.GetValueOrDefault(p.Id))).ToList();
    }

    public Task<UserScore?> GetScoreAsync(int seasonId, string userId, CancellationToken ct = default) =>
        db.UserScores.AsNoTracking().SingleOrDefaultAsync(s => s.SeasonId == seasonId && s.UserId == userId, ct);

    public Task<List<DailySnapshot>> GetHistoryAsync(int seasonId, string userId, CancellationToken ct = default) =>
        db.DailySnapshots.AsNoTracking().Where(s => s.SeasonId == seasonId && s.UserId == userId).OrderBy(s => s.Date).ToListAsync(ct);

    public Task<int> RankedCountAsync(int seasonId, CancellationToken ct = default) =>
        db.UserScores.CountAsync(s => s.SeasonId == seasonId && s.Rank != null, ct);

    /// <summary>Players (incl. pundits) who picked every team; computed live because before lock there are no scores yet.</summary>
    public async Task<int> CompletePlayersAsync(int seasonId, int teamCount, CancellationToken ct = default) =>
        await db.Picks.Where(p => p.SeasonId == seasonId).GroupBy(p => p.UserId).CountAsync(g => g.Count() >= teamCount, ct);

    public async Task<List<PlayerRow>> TopAsync(int seasonId, int take, bool includePundits, CancellationToken ct = default) =>
        await (from s in db.UserScores.AsNoTracking()
               join u in db.Users on s.UserId equals u.Id
               where s.SeasonId == seasonId && s.Rank != null && (includePundits || !u.IsPundit)
               orderby s.Rank, u.DisplayName
               select new PlayerRow(u.Id, u.DisplayName, u.IsPundit, s))
            .Take(take).ToListAsync(ct);
}
