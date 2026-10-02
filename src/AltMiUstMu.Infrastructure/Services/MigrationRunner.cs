using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AltMiUstMu.Infrastructure.Services;

/// <summary>
/// Applies pending EF migrations while holding a Postgres advisory lock, so several app instances (or the
/// web service and the cron service) starting at once never migrate concurrently.
/// </summary>
public class MigrationRunner(AppDbContext db, DatabaseSettings settings, ILogger<MigrationRunner> logger)
{
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        IAsyncDisposable? handle = null;
        try
        {
            handle = await new PostgresAdvisoryLock(settings.ConnectionString).AcquireAsync(LockKeys.Migrate, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            // Database does not exist yet; EF creates it below. Nothing else can be migrating it either.
            logger.LogInformation("Database does not exist yet, it will be created");
        }

        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("Database schema is up to date");
                return;
            }

            logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Migrations applied");
        }
        finally
        {
            if (handle is not null)
            {
                await handle.DisposeAsync();
            }
        }
    }
}

public sealed record DatabaseSettings(string ConnectionString);
