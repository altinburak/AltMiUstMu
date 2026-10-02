using AltMiUstMu.Core.Entities;

namespace AltMiUstMu.Core.Scoring;

public enum PickStatus
{
    /// <summary>No games played yet (or projection exactly on the line).</summary>
    Pending = 0,
    TrendingWin = 1,
    TrendingLoss = 2,
    ClinchedWin = 3,
    ClinchedLoss = 4,

    /// <summary>Whole-number line hit exactly at season end. Worth 0.</summary>
    Push = 5,
}

/// <summary>Current state of one team, the only input the engine needs besides the pick side.</summary>
public readonly record struct TeamState(decimal Line, int Wins, int Losses, int GamesPerTeam)
{
    public int GamesPlayed => Wins + Losses;
    public int Remaining => Math.Max(0, GamesPerTeam - GamesPlayed);
    public int MaxPossibleWins => Wins + Remaining;
}

public readonly record struct PickInput(int TeamId, PickSide Side);

public sealed record ScoreResult(
    int ProjectedCorrect,
    int ClinchedCorrect,
    int ClinchedWrong,
    int TrendingCorrect,
    int TrendingWrong,
    int Pushes,
    int Pending);

public sealed record RankInput(string UserId, int ProjectedCorrect, int ClinchedCorrect, DateTime CompletedAt, string TieName);

public sealed record RankedEntry(string UserId, int Rank);

/// <summary>
/// Pure scoring rules. No I/O, no clock, no framework dependencies.
/// </summary>
public static class ScoringEngine
{
    /// <summary>Projected final wins at the current pace, or null when no games have been played.</summary>
    public static decimal? ProjectedWins(int wins, int losses, int gamesPerTeam)
    {
        var played = wins + losses;
        if (played <= 0)
        {
            return null;
        }

        return Math.Round((decimal)wins / played * gamesPerTeam, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Status of a single pick.</summary>
    public static PickStatus Evaluate(PickSide side, TeamState team)
    {
        var line = team.Line;
        var wins = team.Wins;
        var max = team.MaxPossibleWins;

        // Over: won once wins exceed the line; lost once even winning out cannot exceed it.
        // Under is the mirror image.
        var overWon = wins > line;
        var overLost = max < line;
        var underWon = max < line;
        var underLost = wins > line;

        if (side == PickSide.Over)
        {
            if (overWon) return PickStatus.ClinchedWin;
            if (overLost) return PickStatus.ClinchedLoss;
        }
        else
        {
            if (underWon) return PickStatus.ClinchedWin;
            if (underLost) return PickStatus.ClinchedLoss;
        }

        // Neither side is clinched. If no games remain, the only possibility left is wins == line (whole-number line).
        if (team.Remaining == 0)
        {
            return PickStatus.Push;
        }

        var projected = ProjectedWinsExact(team);
        if (projected is null || projected.Value == line)
        {
            return PickStatus.Pending;
        }

        var overAhead = projected.Value > line;
        return (side == PickSide.Over) == overAhead ? PickStatus.TrendingWin : PickStatus.TrendingLoss;
    }

    public static ScoreResult Score(IEnumerable<PickInput> picks, IReadOnlyDictionary<int, TeamState> teams)
    {
        int clinchedWin = 0, clinchedLoss = 0, trendingWin = 0, trendingLoss = 0, pushes = 0, pending = 0;

        foreach (var pick in picks)
        {
            if (!teams.TryGetValue(pick.TeamId, out var team))
            {
                pending++;
                continue;
            }

            switch (Evaluate(pick.Side, team))
            {
                case PickStatus.ClinchedWin: clinchedWin++; break;
                case PickStatus.ClinchedLoss: clinchedLoss++; break;
                case PickStatus.TrendingWin: trendingWin++; break;
                case PickStatus.TrendingLoss: trendingLoss++; break;
                case PickStatus.Push: pushes++; break;
                default: pending++; break;
            }
        }

        return new ScoreResult(
            ProjectedCorrect: clinchedWin + trendingWin,
            ClinchedCorrect: clinchedWin,
            ClinchedWrong: clinchedLoss,
            TrendingCorrect: trendingWin,
            TrendingWrong: trendingLoss,
            Pushes: pushes,
            Pending: pending);
    }

    /// <summary>
    /// Ranks complete entries. During the season the primary key is the projection; once the season is
    /// finished it is the final (clinched) number of correct picks. Ties: more clinched wins first, then the
    /// earlier completion time. Entries identical on all three keys share a rank (1, 2, 2, 4).
    /// </summary>
    public static IReadOnlyList<RankedEntry> Rank(IEnumerable<RankInput> entries, bool seasonFinished)
    {
        var ordered = entries
            .OrderByDescending(e => seasonFinished ? e.ClinchedCorrect : e.ProjectedCorrect)
            .ThenByDescending(e => e.ClinchedCorrect)
            .ThenBy(e => e.CompletedAt)
            .ThenBy(e => e.TieName, StringComparer.Ordinal)
            .ToList();

        var result = new List<RankedEntry>(ordered.Count);
        RankInput? previous = null;
        var previousRank = 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var rank = previous is not null && SameKeys(previous, current, seasonFinished) ? previousRank : i + 1;
            result.Add(new RankedEntry(current.UserId, rank));
            previous = current;
            previousRank = rank;
        }

        return result;
    }

    /// <summary>True when every team has played the full schedule.</summary>
    public static bool IsSeasonComplete(IEnumerable<(int Wins, int Losses)> records, int gamesPerTeam, int expectedTeams)
    {
        var list = records.ToList();
        return list.Count >= expectedTeams && list.All(r => r.Wins + r.Losses >= gamesPerTeam);
    }

    private static bool SameKeys(RankInput a, RankInput b, bool seasonFinished)
    {
        var primaryA = seasonFinished ? a.ClinchedCorrect : a.ProjectedCorrect;
        var primaryB = seasonFinished ? b.ClinchedCorrect : b.ProjectedCorrect;
        return primaryA == primaryB && a.ClinchedCorrect == b.ClinchedCorrect && a.CompletedAt == b.CompletedAt;
    }

    private static decimal? ProjectedWinsExact(TeamState team)
    {
        if (team.GamesPlayed <= 0)
        {
            return null;
        }

        return (decimal)team.Wins / team.GamesPlayed * team.GamesPerTeam;
    }
}
