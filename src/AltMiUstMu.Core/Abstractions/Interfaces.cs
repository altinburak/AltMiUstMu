namespace AltMiUstMu.Core.Abstractions;

/// <summary>Raw record from an external standings source, keyed by that source's ids.</summary>
public sealed record ProviderTeamRecord(string ExternalId, string Abbreviation, int Wins, int Losses);

/// <summary>Source of current regular-season W-L for every team. Swappable (ESPN today, anything tomorrow).</summary>
public interface IResultsProvider
{
    string Name { get; }

    Task<IReadOnlyList<ProviderTeamRecord>> GetStandingsAsync(int seasonEndYear, CancellationToken ct);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string html, CancellationToken ct = default);
}

/// <summary>Cross-process mutual exclusion (Postgres advisory lock in production).</summary>
public interface IDistributedLock
{
    /// <summary>Returns a handle when acquired, or null if someone else holds the lock.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(long key, CancellationToken ct);
}

public static class LockKeys
{
    public const long Sync = 0x414C5453594E43; // "ALTSYNC"
    public const long Migrate = 0x414C544D4947; // "ALTMIG"
}

/// <summary>Notified after scores change so cached public pages can be dropped.</summary>
public interface ICacheInvalidator
{
    Task InvalidateAsync(CancellationToken ct = default);

    /// <summary>Evicts one player's cached public profile.</summary>
    Task InvalidatePlayerAsync(string userId, CancellationToken ct = default);
}

public sealed class NoopCacheInvalidator : ICacheInvalidator
{
    public Task InvalidateAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task InvalidatePlayerAsync(string userId, CancellationToken ct = default) => Task.CompletedTask;
}
