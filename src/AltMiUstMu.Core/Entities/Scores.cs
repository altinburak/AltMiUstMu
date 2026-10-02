namespace AltMiUstMu.Core.Entities;

/// <summary>
/// Precomputed leaderboard row, rewritten in bulk after every sync / admin change.
/// Pages read this table only; they never recompute scores.
/// </summary>
public class UserScore
{
    public int SeasonId { get; set; }
    public string UserId { get; set; } = "";
    public int PicksCount { get; set; }
    public bool IsComplete { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProjectedCorrect { get; set; }
    public int ClinchedCorrect { get; set; }
    public int ClinchedWrong { get; set; }
    public int TrendingCorrect { get; set; }
    public int TrendingWrong { get; set; }
    public int Pushes { get; set; }

    /// <summary>Null when the user is not ranked (incomplete picks).</summary>
    public int? Rank { get; set; }

    /// <summary>Rank in the most recent snapshot from a previous day, for movement arrows.</summary>
    public int? PreviousRank { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class DailySnapshot
{
    public long Id { get; set; }
    public int SeasonId { get; set; }
    public string UserId { get; set; } = "";
    public DateOnly Date { get; set; }
    public int ProjectedCorrect { get; set; }
    public int ClinchedCorrect { get; set; }
    public int? Rank { get; set; }
}

public class SyncRun
{
    public long Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public SyncRunStatus Status { get; set; }
    public SyncTrigger Trigger { get; set; }
    public string Message { get; set; } = "";
    public int UpdatedTeams { get; set; }
}

public class AdminAuditLog
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ActorUserId { get; set; }
    public string ActorName { get; set; } = "";

    /// <summary>Machine friendly category, e.g. "line", "pundit-pick", "manual-record", "season", "user".</summary>
    public string Action { get; set; } = "";

    public string Target { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
