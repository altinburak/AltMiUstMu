using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IDataProtectionKeyContext
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamLine> TeamLines => Set<TeamLine>();
    public DbSet<TeamRecord> TeamRecords => Set<TeamRecord>();
    public DbSet<Pick> Picks => Set<Pick>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<UserScore> UserScores => Set<UserScore>();
    public DbSet<DailySnapshot> DailySnapshots => Set<DailySnapshot>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(40).IsRequired();
            e.Property(u => u.DisplayNameKey).HasMaxLength(40).IsRequired();
            e.HasIndex(u => u.DisplayNameKey).IsUnique();
            e.HasIndex(u => u.IsPundit);
        });

        builder.Entity<Season>(e =>
        {
            e.Property(s => s.Label).HasMaxLength(20).IsRequired();
            e.HasIndex(s => s.Label).IsUnique();
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            e.Ignore(s => s.EndYear);
        });

        builder.Entity<Team>(e =>
        {
            e.Property(t => t.Abbreviation).HasMaxLength(4).IsRequired();
            e.HasIndex(t => t.Abbreviation).IsUnique();
            e.Property(t => t.City).HasMaxLength(40);
            e.Property(t => t.Name).HasMaxLength(40);
            e.Property(t => t.Division).HasMaxLength(20);
            e.Property(t => t.PrimaryColor).HasMaxLength(7);
            e.Property(t => t.SecondaryColor).HasMaxLength(7);
            e.Property(t => t.EspnId).HasMaxLength(10);
            e.Property(t => t.Conference).HasConversion<string>().HasMaxLength(8);
            e.Ignore(t => t.FullName);
        });

        builder.Entity<TeamLine>(e =>
        {
            e.HasKey(l => new { l.SeasonId, l.TeamId });
            e.Property(l => l.Line).HasPrecision(5, 1);
            e.HasOne<Season>().WithMany().HasForeignKey(l => l.SeasonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Team).WithMany().HasForeignKey(l => l.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeamRecord>(e =>
        {
            e.HasKey(r => new { r.SeasonId, r.TeamId });
            e.Property(r => r.Source).HasConversion<string>().HasMaxLength(8);
            e.HasOne<Season>().WithMany().HasForeignKey(r => r.SeasonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Team).WithMany().HasForeignKey(r => r.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(r => r.GamesPlayed);
        });

        builder.Entity<Pick>(e =>
        {
            e.HasIndex(p => new { p.UserId, p.SeasonId, p.TeamId }).IsUnique();
            e.HasIndex(p => new { p.SeasonId, p.TeamId });
            e.Property(p => p.Side).HasConversion<string>().HasMaxLength(8);
            e.HasOne<AppUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Season>().WithMany().HasForeignKey(p => p.SeasonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Team).WithMany().HasForeignKey(p => p.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Group>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(60).IsRequired();
            e.Property(g => g.Slug).HasMaxLength(60).IsRequired();
            e.Property(g => g.InviteCode).HasMaxLength(16).IsRequired();
            e.HasIndex(g => g.Slug).IsUnique();
            e.HasIndex(g => g.InviteCode).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(g => g.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Season>().WithMany().HasForeignKey(g => g.SeasonId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GroupMember>(e =>
        {
            e.HasKey(m => new { m.GroupId, m.UserId });
            e.HasIndex(m => m.UserId);
            e.HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserScore>(e =>
        {
            e.HasKey(s => new { s.SeasonId, s.UserId });
            e.HasIndex(s => new { s.SeasonId, s.Rank });
            e.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Season>().WithMany().HasForeignKey(s => s.SeasonId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DailySnapshot>(e =>
        {
            e.HasIndex(s => new { s.SeasonId, s.UserId, s.Date }).IsUnique();
            e.HasIndex(s => new { s.SeasonId, s.Date });
            e.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Season>().WithMany().HasForeignKey(s => s.SeasonId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SyncRun>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Trigger).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Message).HasMaxLength(4000);
            e.HasIndex(r => r.StartedAt);
        });

        builder.Entity<AdminAuditLog>(e =>
        {
            e.Property(a => a.ActorName).HasMaxLength(100);
            e.Property(a => a.Action).HasMaxLength(40);
            e.Property(a => a.Target).HasMaxLength(200);
            e.Property(a => a.OldValue).HasMaxLength(500);
            e.Property(a => a.NewValue).HasMaxLength(500);
            e.HasIndex(a => a.CreatedAt);
        });
    }
}
