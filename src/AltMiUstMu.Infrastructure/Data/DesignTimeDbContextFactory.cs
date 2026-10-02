using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AltMiUstMu.Infrastructure.Data;

/// <summary>Used only by `dotnet ef` to create migrations; never connects anywhere.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=altmiustmu;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
