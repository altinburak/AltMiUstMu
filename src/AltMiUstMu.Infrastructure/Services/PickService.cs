using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Infrastructure.Services;

public enum PickResult
{
    Saved,
    Unchanged,
    Locked,
    NoSeason,
    InvalidTeam,
    NotAllowed,
}

public class PickService(AppDbContext db, TimeProvider time, AuditService audit, ScoreService scores, ICacheInvalidator cache)
{
    /// <summary>
    /// Creates or changes a regular user's pick. The lock is enforced here, server side, regardless of the UI.
    /// </summary>
    public async Task<PickResult> SetPickAsync(string userId, int teamId, PickSide side, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (season is null)
        {
            return PickResult.NoSeason;
        }

        var now = time.GetUtcNow().UtcDateTime;
        if (PickRules.IsLocked(season, now))
        {
            return PickResult.Locked;
        }

        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.IsPundit, u.IsDisabled, u.PicksSharedSeasonId }).SingleOrDefaultAsync(ct);
        if (user is null || user.IsPundit || user.IsDisabled)
        {
            return PickResult.NotAllowed;
        }

        if (!await db.TeamLines.AnyAsync(l => l.SeasonId == season.Id && l.TeamId == teamId, ct))
        {
            return PickResult.InvalidTeam;
        }

        var result = await UpsertAsync(userId, season.Id, teamId, side, now, ct);
        if (result == PickResult.Saved && user.PicksSharedSeasonId == season.Id)
        {
            await cache.InvalidatePlayerAsync(userId, ct);
        }

        return result;
    }

    /// <summary>
    /// Called when the user shares their picks: from then on the current season's picks are public on their
    /// profile, even before the lock. Returns false when there is no season or the account can't pick.
    /// </summary>
    public async Task<bool> MarkPicksSharedAsync(string userId, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (season is null || user is null || user.IsPundit || user.IsDisabled)
        {
            return false;
        }

        if (user.PicksSharedSeasonId != season.Id)
        {
            user.PicksSharedSeasonId = season.Id;
            await db.SaveChangesAsync(ct);
            await cache.InvalidatePlayerAsync(userId, ct);
        }

        return true;
    }

    /// <summary>
    /// Admin-only: sets (or clears, when <paramref name="side"/> is null) a pundit pick. Allowed after lock;
    /// every change is audited and scores are recomputed when the season is already running.
    /// </summary>
    public async Task<int> SetPunditPicksAsync(AuditActor actor, string punditId, IReadOnlyDictionary<int, PickSide?> picks, CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking().OrderByDescending(s => s.Id).FirstAsync(ct);
        var pundit = await db.Users.SingleAsync(u => u.Id == punditId && u.IsPundit, ct);
        var teams = await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var existing = await db.Picks.Where(p => p.UserId == punditId && p.SeasonId == season.Id).ToDictionaryAsync(p => p.TeamId, ct);
        var now = time.GetUtcNow().UtcDateTime;
        var changes = 0;

        foreach (var (teamId, side) in picks)
        {
            if (!teams.TryGetValue(teamId, out var team))
            {
                continue;
            }

            existing.TryGetValue(teamId, out var current);
            if (current?.Side == side)
            {
                continue;
            }

            var target = $"{pundit.DisplayName} / {team.Abbreviation}";
            if (side is null)
            {
                if (current is null)
                {
                    continue;
                }

                db.Picks.Remove(current);
                audit.Log(actor, "pundit-pick", target, SideLabel(current.Side), null);
            }
            else if (current is null)
            {
                db.Picks.Add(new Pick { UserId = punditId, SeasonId = season.Id, TeamId = teamId, Side = side.Value, CreatedAt = now, UpdatedAt = now });
                audit.Log(actor, "pundit-pick", target, null, SideLabel(side.Value));
            }
            else
            {
                audit.Log(actor, "pundit-pick", target, SideLabel(current.Side), SideLabel(side.Value));
                current.Side = side.Value;
                current.UpdatedAt = now;
            }

            changes++;
        }

        if (changes > 0)
        {
            await db.SaveChangesAsync(ct);
            await scores.RecomputeAsync(season.Id, writeSnapshot: false, ct);
            await cache.InvalidateAsync(ct);
        }

        return changes;
    }

    public static string SideLabel(PickSide side) => side == PickSide.Over ? "ÜST" : "ALT";

    private async Task<PickResult> UpsertAsync(string userId, int seasonId, int teamId, PickSide side, DateTime now, CancellationToken ct)
    {
        var pick = await db.Picks.SingleOrDefaultAsync(p => p.UserId == userId && p.SeasonId == seasonId && p.TeamId == teamId, ct);
        if (pick is not null)
        {
            if (pick.Side == side)
            {
                return PickResult.Unchanged;
            }

            pick.Side = side;
            pick.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return PickResult.Saved;
        }

        db.Picks.Add(new Pick { UserId = userId, SeasonId = seasonId, TeamId = teamId, Side = side, CreatedAt = now, UpdatedAt = now });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Double click raced another request that inserted the same pick: fall back to an update.
            db.ChangeTracker.Clear();
            var again = await db.Picks.SingleAsync(p => p.UserId == userId && p.SeasonId == seasonId && p.TeamId == teamId, ct);
            again.Side = side;
            again.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }

        return PickResult.Saved;
    }
}
