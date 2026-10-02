using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Scoring;
using AltMiUstMu.Core.Sync;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AltMiUstMu.Infrastructure.Services;

public sealed record SyncOutcome(bool Success, string Message, long? RunId);

/// <summary>
/// Daily results sync. The same code path runs from the CLI (Railway cron), the cron HTTP endpoint and the
/// admin button. Flow: advisory lock -> fetch -> validate (abort without writes on any problem) -> one
/// transaction (records + season status + scores + snapshots) -> cache invalidation.
/// </summary>
public class SyncService(
    AppDbContext db,
    IResultsProvider provider,
    IDistributedLock distributedLock,
    ScoreService scores,
    ICacheInvalidator cache,
    TimeProvider time,
    ILogger<SyncService> logger)
{
    public async Task<SyncOutcome> RunAsync(SyncTrigger trigger, CancellationToken ct = default)
    {
        await using var handle = await distributedLock.TryAcquireAsync(LockKeys.Sync, ct);
        if (handle is null)
        {
            const string busy = "Başka bir senkronizasyon zaten çalışıyor; bu çalıştırma atlandı.";
            logger.LogWarning("Sync skipped: lock held by another process");
            var skipped = await StartRunAsync(trigger, SyncRunStatus.Skipped, busy, ct);
            return new SyncOutcome(false, busy, skipped.Id);
        }

        var run = await StartRunAsync(trigger, SyncRunStatus.Running, "", ct);
        try
        {
            var outcome = await RunLockedAsync(run, ct);
            if (outcome.Success)
            {
                await cache.InvalidateAsync(ct);
            }

            return outcome;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Sync failed");
            return await FailAsync(run.Id, $"Beklenmeyen hata: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task<SyncOutcome> RunLockedAsync(SyncRun run, CancellationToken ct)
    {
        var season = await db.Seasons.OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (season is null)
        {
            return await FailAsync(run.Id, "Sezon bulunamadı. Önce 'seed' komutunu çalıştırın.");
        }

        var teams = await db.Teams.AsNoTracking().ToListAsync(ct);
        var records = await db.TeamRecords.Where(r => r.SeasonId == season.Id).ToDictionaryAsync(r => r.TeamId, ct);

        IReadOnlyList<ProviderTeamRecord> raw;
        try
        {
            raw = await provider.GetStandingsAsync(season.EndYear, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or System.Text.Json.JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "Results provider {Provider} failed", provider.Name);
            return await FailAsync(run.Id, $"{provider.Name} verisi alınamadı: {ex.Message}");
        }

        var incoming = TeamMapper.Map(raw, teams);
        var existing = teams.ToDictionary(
            t => t.Id,
            t => records.TryGetValue(t.Id, out var r)
                ? new ExistingRecord(r.Wins, r.Losses, r.Source == RecordSource.Manual)
                : new ExistingRecord(0, 0, false));

        var validation = SyncValidator.Validate(incoming, existing, season.GamesPerTeam, teams.Count);
        if (!validation.IsValid)
        {
            logger.LogError("Sync validation failed: {Errors}", string.Join(" | ", validation.Errors));
            return await FailAsync(run.Id, "Doğrulama başarısız, hiçbir şey yazılmadı: " + string.Join(" | ", validation.Errors));
        }

        var now = time.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var updated = 0;
        var skippedManual = 0;
        foreach (var item in incoming)
        {
            var teamId = item.TeamId!.Value;
            if (!records.TryGetValue(teamId, out var record))
            {
                record = new TeamRecord { SeasonId = season.Id, TeamId = teamId, Source = RecordSource.Api };
                db.TeamRecords.Add(record);
                records[teamId] = record;
            }
            else if (record.Source == RecordSource.Manual)
            {
                skippedManual++;
                continue;
            }

            if (record.Wins != item.Wins || record.Losses != item.Losses)
            {
                updated++;
            }

            record.Wins = item.Wins;
            record.Losses = item.Losses;
            record.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        var allDone = ScoringEngine.IsSeasonComplete(records.Values.Select(r => (r.Wins, r.Losses)), season.GamesPerTeam, teams.Count);
        var newStatus = PickRules.ExpectedStatus(season, now, allDone);
        var statusNote = "";
        if (newStatus != season.Status)
        {
            statusNote = $" Sezon durumu: {season.Status} -> {newStatus}.";
            logger.LogInformation("Season {Label} status {Old} -> {New}", season.Label, season.Status, newStatus);
            season.Status = newStatus;
            await db.SaveChangesAsync(ct);
        }

        var users = await scores.RecomputeAsync(season.Id, writeSnapshot: true, ct);

        var message = $"{incoming.Count} takım alındı, {updated} kayıt değişti, {skippedManual} manuel kayıt atlandı, {users} oyuncunun puanı hesaplandı.{statusNote}";
        var tracked = await db.SyncRuns.SingleAsync(r => r.Id == run.Id, ct);
        tracked.Status = SyncRunStatus.Succeeded;
        tracked.FinishedAt = time.GetUtcNow().UtcDateTime;
        tracked.Message = message;
        tracked.UpdatedTeams = updated;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Sync succeeded: {Message}", message);
        return new SyncOutcome(true, message, run.Id);
    }

    private async Task<SyncRun> StartRunAsync(SyncTrigger trigger, SyncRunStatus status, string message, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var run = new SyncRun
        {
            StartedAt = now,
            FinishedAt = status == SyncRunStatus.Running ? null : now,
            Status = status,
            Trigger = trigger,
            Message = message,
        };
        db.SyncRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    private async Task<SyncOutcome> FailAsync(long runId, string message)
    {
        // Drop anything half-done from the change tracker; the failed run is recorded on its own.
        db.ChangeTracker.Clear();
        var run = await db.SyncRuns.SingleAsync(r => r.Id == runId, CancellationToken.None);
        run.Status = SyncRunStatus.Failed;
        run.FinishedAt = time.GetUtcNow().UtcDateTime;
        run.Message = message.Length > 4000 ? message[..4000] : message;
        await db.SaveChangesAsync(CancellationToken.None);
        return new SyncOutcome(false, message, runId);
    }
}
