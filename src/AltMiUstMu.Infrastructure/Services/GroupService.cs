using System.Security.Cryptography;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Infrastructure.Services;

public sealed record GroupResult(bool Success, string? Error = null, Group? Group = null)
{
    public static GroupResult Fail(string error) => new(false, error);
}

public class GroupService(AppDbContext db, TimeProvider time)
{
    public const int MaxOwnedGroups = 10;
    public const int MaxMemberships = 30;
    public const int MaxMembers = 500;

    // No 0/O/1/I/L to keep codes readable when dictated.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public async Task<GroupResult> CreateAsync(string userId, string rawName, CancellationToken ct = default)
    {
        var errors = NameRules.ValidateGroupName(rawName);
        if (errors.Count > 0)
        {
            return GroupResult.Fail(errors[0]);
        }

        var season = await db.Seasons.AsNoTracking().OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (season is null)
        {
            return GroupResult.Fail("Aktif sezon bulunamadı.");
        }

        if (await db.Groups.CountAsync(g => g.OwnerId == userId, ct) >= MaxOwnedGroups)
        {
            return GroupResult.Fail($"En fazla {MaxOwnedGroups} grup kurabilirsin.");
        }

        if (await db.GroupMembers.CountAsync(m => m.UserId == userId, ct) >= MaxMemberships)
        {
            return GroupResult.Fail($"En fazla {MaxMemberships} gruba üye olabilirsin.");
        }

        var name = TextNormalizer.CleanDisplay(rawName);
        var now = time.GetUtcNow().UtcDateTime;
        var group = new Group
        {
            Name = name,
            Slug = $"{TextNormalizer.Slugify(name)}-{RandomCode(4).ToLowerInvariant()}",
            InviteCode = RandomCode(8),
            OwnerId = userId,
            SeasonId = season.Id,
            CreatedAt = now,
        };
        group.Members.Add(new GroupMember { UserId = userId, JoinedAt = now });
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);
        return new GroupResult(true, Group: group);
    }

    public async Task<GroupResult> JoinAsync(string userId, string inviteCode, CancellationToken ct = default)
    {
        var code = (inviteCode ?? "").Trim().ToUpperInvariant();
        var group = await db.Groups.Include(g => g.Members).SingleOrDefaultAsync(g => g.InviteCode == code, ct);
        if (group is null)
        {
            return GroupResult.Fail("Davet kodu geçersiz.");
        }

        if (group.Members.Any(m => m.UserId == userId))
        {
            return new GroupResult(true, Group: group);
        }

        if (group.Members.Count >= MaxMembers)
        {
            return GroupResult.Fail("Bu grup dolu.");
        }

        if (await db.GroupMembers.CountAsync(m => m.UserId == userId, ct) >= MaxMemberships)
        {
            return GroupResult.Fail($"En fazla {MaxMemberships} gruba üye olabilirsin.");
        }

        group.Members.Add(new GroupMember { UserId = userId, JoinedAt = time.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(ct);
        return new GroupResult(true, Group: group);
    }

    /// <summary>Leaving as the owner hands the group to the longest-standing member, or deletes it if empty.</summary>
    public async Task<GroupResult> LeaveAsync(string userId, int groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.Include(g => g.Members).SingleOrDefaultAsync(g => g.Id == groupId, ct);
        var membership = group?.Members.SingleOrDefault(m => m.UserId == userId);
        if (group is null || membership is null)
        {
            return GroupResult.Fail("Bu grubun üyesi değilsin.");
        }

        group.Members.Remove(membership);
        if (group.OwnerId == userId)
        {
            var heir = group.Members.OrderBy(m => m.JoinedAt).FirstOrDefault();
            if (heir is null)
            {
                db.Groups.Remove(group);
            }
            else
            {
                group.OwnerId = heir.UserId;
            }
        }

        await db.SaveChangesAsync(ct);
        return new GroupResult(true, Group: group);
    }

    public async Task<GroupResult> RemoveMemberAsync(string ownerId, int groupId, string memberId, CancellationToken ct = default)
    {
        var group = await db.Groups.Include(g => g.Members).SingleOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null || group.OwnerId != ownerId)
        {
            return GroupResult.Fail("Bu işlem için grup sahibi olmalısın.");
        }

        if (memberId == ownerId)
        {
            return GroupResult.Fail("Kendini çıkaramazsın; grubu silebilir ya da gruptan ayrılabilirsin.");
        }

        var member = group.Members.SingleOrDefault(m => m.UserId == memberId);
        if (member is null)
        {
            return GroupResult.Fail("Üye bulunamadı.");
        }

        group.Members.Remove(member);
        await db.SaveChangesAsync(ct);
        return new GroupResult(true, Group: group);
    }

    public async Task<GroupResult> DeleteAsync(string ownerId, int groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.SingleOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null || group.OwnerId != ownerId)
        {
            return GroupResult.Fail("Bu işlem için grup sahibi olmalısın.");
        }

        db.Groups.Remove(group);
        await db.SaveChangesAsync(ct);
        return new GroupResult(true);
    }

    public static string RandomCode(int length) =>
        string.Create(length, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }
        });
}
