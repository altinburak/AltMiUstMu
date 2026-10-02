using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Services;

namespace AltMiUstMu.Web;

/// <summary>
/// One-shot commands sharing the web app's configuration and DI container:
/// <c>dotnet AltMiUstMu.Web.dll sync|migrate|seed|seed-demo</c>. Exit code 0 = success, 1 = failure.
/// </summary>
public static class CliRunner
{
    private static readonly string[] Commands = ["sync", "migrate", "seed", "seed-demo"];

    public static string? GetCommand(string[] args) =>
        args.Length > 0 && Commands.Contains(args[0], StringComparer.OrdinalIgnoreCase) ? args[0].ToLowerInvariant() : null;

    public static async Task<int> RunAsync(WebApplication app, string command)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AltMiUstMu.Cli");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        try
        {
            if (command == "seed-demo" && app.Environment.IsProduction())
            {
                logger.LogError("seed-demo refuses to run when ASPNETCORE_ENVIRONMENT=Production");
                return 1;
            }

            await using var scope = app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            await services.GetRequiredService<MigrationRunner>().MigrateAsync(cts.Token);

            switch (command)
            {
                case "migrate":
                    return 0;

                case "seed":
                    await services.GetRequiredService<SeedService>().SeedAsync(cts.Token);
                    return 0;

                case "seed-demo":
                    await services.GetRequiredService<DemoSeedService>().SeedAsync(cts.Token);
                    return 0;

                case "sync":
                    var outcome = await services.GetRequiredService<SyncService>().RunAsync(SyncTrigger.Cli, cts.Token);
                    if (outcome.Success)
                    {
                        logger.LogInformation("Sync OK: {Message}", outcome.Message);
                        return 0;
                    }

                    logger.LogError("Sync FAILED: {Message}", outcome.Message);
                    return 1;

                default:
                    logger.LogError("Unknown command {Command}", command);
                    return 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Command {Command} failed", command);
            return 1;
        }
        finally
        {
            await Serilog.Log.CloseAndFlushAsync();
        }
    }
}
