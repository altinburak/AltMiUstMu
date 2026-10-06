using AltMiUstMu.Core.Entities;

namespace AltMiUstMu.Core.Scoring;

public static class PickRules
{
    public const int TeamCount = 30;

    /// <summary>
    /// Picks are editable only while the season is Upcoming and the lock moment has not passed.
    /// Both conditions are checked so a stale status or a moved LockAt can never reopen picks by accident.
    /// </summary>
    public static bool IsLocked(Season season, DateTime utcNow) =>
        season.Status != SeasonStatus.Upcoming || utcNow >= season.LockAt;

    /// <summary>Whether <paramref name="viewerId"/> may see the picks of <paramref name="ownerId"/>.</summary>
    public static bool CanViewPicks(Season season, DateTime utcNow, string ownerId, bool ownerIsPundit, string? viewerId, bool viewerIsAdmin = false, bool ownerShared = false) =>
        ownerIsPundit
        || ownerShared
        || viewerIsAdmin
        || IsLocked(season, utcNow)
        || (viewerId is not null && viewerId == ownerId);

    /// <summary>Season status implied by the clock and the records. Never moves a Finished season back.</summary>
    public static SeasonStatus ExpectedStatus(Season season, DateTime utcNow, bool allTeamsFinished)
    {
        if (season.Status == SeasonStatus.Finished)
        {
            return SeasonStatus.Finished;
        }

        if (utcNow < season.LockAt)
        {
            return season.Status;
        }

        return allTeamsFinished ? SeasonStatus.Finished : SeasonStatus.Active;
    }
}
