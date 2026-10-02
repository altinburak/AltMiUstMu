using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Data;

namespace AltMiUstMu.Infrastructure.Services;

public sealed record AuditActor(string? UserId, string Name)
{
    public static readonly AuditActor System = new(null, "Sistem");
}

/// <summary>Adds audit rows to the current unit of work; they are saved with the change they describe.</summary>
public class AuditService(AppDbContext db, TimeProvider time)
{
    public void Log(AuditActor actor, string action, string target, string? oldValue, string? newValue)
    {
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            CreatedAt = time.GetUtcNow().UtcDateTime,
            ActorUserId = actor.UserId,
            ActorName = Truncate(actor.Name, 100),
            Action = Truncate(action, 40),
            Target = Truncate(target, 200),
            OldValue = oldValue is null ? null : Truncate(oldValue, 500),
            NewValue = newValue is null ? null : Truncate(newValue, 500),
        });
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
