using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Core.Time;
using AltMiUstMu.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Infrastructure.Services;

/// <summary>
/// Recomputes every user's score for a season in one pass and stores it in UserScore (+ DailySnapshot).
/// Does not call SaveChanges' transaction itself: callers decide the unit of work.
/// </summary>
public class ScoreService(AppDbContext db, TimeProvider time)
{
    public async Task<int> RecomputeAsync(int seasonId, bool writeSnapshot, CancellationToken ct = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var today = Istanbul.Today(now);
        var season = await db.Seasons.SingleAsync(s => s.Id == seasonId, ct);
        var teams = await LoadTeamStatesAsync(season, ct);

        var picks = await db.Picks.AsNoTracking()
            .Where(p => p.SeasonId == seasonId)
            .Select(p => new { p.UserId, p.TeamId, p.Side, p.CreatedAt })
            .ToListAsync(ct);

        var disabled = (await db.Users.AsNoTracking().Where(u => u.IsDisabled).Select(u => u.Id).ToListAsync(ct)).ToHashSet();
        var names = await db.Users.AsNoTracking().Select(u => new { u.Id, u.DisplayNameKey }).ToDictionaryAsync(u => u.Id, u => u.DisplayNameKey, ct);

        var teamCount = teams.Count == 0 ? PickRules.TeamCount : teams.Count;
        var computed = picks
            .GroupBy(p => p.UserId)
            .Select(g =>
            {
                var list = g.ToList();
                var score = ScoringEngine.Score(list.Select(p => new PickInput(p.TeamId, p.Side)), teams);
                var complete = list.Count >= teamCount;
                return new
                {
                    UserId = g.Key,
                    Count = list.Count,
                    Complete = complete,
                    CompletedAt = complete ? list.Max(p => p.CreatedAt) : (DateTime?)null,
                    Score = score,
                };
            })
            .ToList();

        var ranks = ScoringEngine.Rank(
                computed.Where(c => c.Complete && !disabled.Contains(c.UserId))
                    .Select(c => new RankInput(c.UserId, c.Score.ProjectedCorrect, c.Score.ClinchedCorrect, c.CompletedAt!.Value, names.GetValueOrDefault(c.UserId, c.UserId))),
                season.Status == SeasonStatus.Finished)
            .ToDictionary(r => r.UserId, r => r.Rank);

        // Rank from the most recent snapshot of an earlier day drives the movement arrows.
        var previousDate = await db.DailySnapshots.Where(s => s.SeasonId == seasonId && s.Date < today)
            .MaxAsync(s => (DateOnly?)s.Date, ct);
        var previousRanks = previousDate is null
            ? []
            : await db.DailySnapshots.AsNoTracking()
                .Where(s => s.SeasonId == seasonId && s.Date == previousDate)
                .ToDictionaryAsync(s => s.UserId, s => s.Rank, ct);

        var existing = await db.UserScores.Where(s => s.SeasonId == seasonId).ToDictionaryAsync(s => s.UserId, ct);
        var seen = new HashSet<string>();

        foreach (var c in computed)
        {
            seen.Add(c.UserId);
            if (!existing.TryGetValue(c.UserId, out var row))
            {
                row = new UserScore { SeasonId = seasonId, UserId = c.UserId };
                db.UserScores.Add(row);
            }

            row.PicksCount = c.Count;
            row.IsComplete = c.Complete;
            row.CompletedAt = c.CompletedAt;
            row.ProjectedCorrect = c.Score.ProjectedCorrect;
            row.ClinchedCorrect = c.Score.ClinchedCorrect;
            row.ClinchedWrong = c.Score.ClinchedWrong;
            row.TrendingCorrect = c.Score.TrendingCorrect;
            row.TrendingWrong = c.Score.TrendingWrong;
            row.Pushes = c.Score.Pushes;
            row.Rank = ranks.TryGetValue(c.UserId, out var rank) ? rank : null;
            row.PreviousRank = previousRanks.GetValueOrDefault(c.UserId);
            row.UpdatedAt = now;
        }

        foreach (var stale in existing.Values.Where(s => !seen.Contains(s.UserId)))
        {
            db.UserScores.Remove(stale);
        }

        if (writeSnapshot && season.Status != SeasonStatus.Upcoming)
        {
            var todays = await db.DailySnapshots.Where(s => s.SeasonId == seasonId && s.Date == today)
                .ToDictionaryAsync(s => s.UserId, ct);
            foreach (var c in computed.Where(c => c.Complete))
            {
                if (!todays.TryGetValue(c.UserId, out var snap))
                {
                    snap = new DailySnapshot { SeasonId = seasonId, UserId = c.UserId, Date = today };
                    db.DailySnapshots.Add(snap);
                }

                snap.ProjectedCorrect = c.Score.ProjectedCorrect;
                snap.ClinchedCorrect = c.Score.ClinchedCorrect;
                snap.Rank = ranks.TryGetValue(c.UserId, out var r) ? r : null;
            }
        }

        await db.SaveChangesAsync(ct);
        return computed.Count;
    }

    public async Task<Dictionary<int, TeamState>> LoadTeamStatesAsync(Season season, CancellationToken ct = default)
    {
        var lines = await db.TeamLines.AsNoTracking().Where(l => l.SeasonId == season.Id).ToListAsync(ct);
        var records = await db.TeamRecords.AsNoTracking().Where(r => r.SeasonId == season.Id).ToDictionaryAsync(r => r.TeamId, ct);
        return lines.ToDictionary(
            l => l.TeamId,
            l => records.TryGetValue(l.TeamId, out var r)
                ? new TeamState(l.Line, r.Wins, r.Losses, season.GamesPerTeam)
                : new TeamState(l.Line, 0, 0, season.GamesPerTeam));
    }
}
