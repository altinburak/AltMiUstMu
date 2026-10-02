using AltMiUstMu.Core.Abstractions;
using Npgsql;

namespace AltMiUstMu.Infrastructure.Data;

/// <summary>
/// Session-level Postgres advisory lock held on a dedicated connection, so it is independent of any
/// DbContext transaction and is released automatically if the process dies.
/// </summary>
public sealed class PostgresAdvisoryLock(string connectionString) : IDistributedLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(long key, CancellationToken ct)
    {
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@k)", conn);
            cmd.Parameters.AddWithValue("k", key);
            var acquired = (bool)(await cmd.ExecuteScalarAsync(ct))!;
            if (!acquired)
            {
                await conn.DisposeAsync();
                return null;
            }

            return new Handle(conn, key);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    /// <summary>Blocks until the lock is available (used for migrations so replicas wait instead of skipping).</summary>
    public async Task<IAsyncDisposable> AcquireAsync(long key, CancellationToken ct)
    {
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT pg_advisory_lock(@k)", conn);
            cmd.Parameters.AddWithValue("k", key);
            cmd.CommandTimeout = 0;
            await cmd.ExecuteNonQueryAsync(ct);
            return new Handle(conn, key);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private sealed class Handle(NpgsqlConnection conn, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@k)", conn);
                cmd.Parameters.AddWithValue("k", key);
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                await conn.DisposeAsync();
            }
        }
    }
}
