using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Email;
using AltMiUstMu.Infrastructure.Results;
using AltMiUstMu.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace AltMiUstMu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = ConnectionStringResolver.Resolve(config);
        services.AddSingleton(new DatabaseSettings(connectionString));
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
        services.AddSingleton<IDistributedLock>(new PostgresAdvisoryLock(connectionString));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICacheInvalidator, NoopCacheInvalidator>();

        services.AddScoped<SeasonService>();
        services.AddScoped<AuditService>();
        services.AddScoped<ScoreService>();
        services.AddScoped<SyncService>();
        services.AddScoped<PickService>();
        services.AddScoped<GroupService>();
        services.AddScoped<MigrationRunner>();
        services.AddScoped<SeedService>();
        services.AddScoped<DemoSeedService>();
        services.AddScoped<AdminService>();

        services.AddHttpClient<IResultsProvider, EspnResultsProvider>(c =>
            {
                c.BaseAddress = new Uri(EspnResultsProvider.BaseUrl);
                c.Timeout = TimeSpan.FromSeconds(60);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("AltMiUstMu/1.0 (+https://github.com)");
            })
            .AddResilienceHandler("espn", b =>
            {
                b.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(2),
                });
                b.AddTimeout(TimeSpan.FromSeconds(15));
            });

        services.Configure<EmailOptions>(o =>
        {
            o.ResendApiKey = config["RESEND_API_KEY"];
            if (!string.IsNullOrWhiteSpace(config["EMAIL_FROM"]))
            {
                o.From = config["EMAIL_FROM"]!;
            }
        });

        if (string.IsNullOrWhiteSpace(config["RESEND_API_KEY"]))
        {
            services.AddSingleton<IEmailService, LoggingEmailService>();
        }
        else
        {
            // No retries: a retried POST could deliver the same email twice.
            services.AddHttpClient<IEmailService, ResendEmailService>(c =>
            {
                c.BaseAddress = new Uri("https://api.resend.com/");
                c.Timeout = TimeSpan.FromSeconds(20);
            });
        }

        return services;
    }
}
