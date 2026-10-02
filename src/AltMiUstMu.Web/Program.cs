using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web;
using AltMiUstMu.Web.Identity;
using AltMiUstMu.Web.Infrastructure;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.WebEncoders;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

Console.OutputEncoding = Encoding.UTF8;
var turkish = CultureInfo.GetCultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkish;
CultureInfo.DefaultThreadCurrentUICulture = turkish;

var command = CliRunner.GetCommand(args);
var builder = WebApplication.CreateBuilder(command is null ? args : args.Skip(1).ToArray());

// ---------- Logging ----------
builder.Services.AddSerilog((services, lc) =>
{
    lc.ReadFrom.Configuration(builder.Configuration)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
        .MinimumLevel.Override("Polly", LogEventLevel.Warning)
        .Enrich.FromLogContext();
    if (builder.Environment.IsProduction())
    {
        lc.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
    else
    {
        lc.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}", formatProvider: CultureInfo.InvariantCulture);
    }
});

// ---------- Railway: PORT ----------
var port = builder.Configuration["PORT"];
if (command is null && !string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ---------- Services ----------
builder.Services.AddPublicPageCache(); // registers ICacheInvalidator before Infrastructure's no-op fallback
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMemoryCache();
// Emit Turkish characters (ı, ş, ğ, ü, ö, ç, İ) as-is instead of numeric HTML entities.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GameQueries>();
builder.Services.AddScoped<AccountEmails>();

builder.Services.AddDataProtection()
    .SetApplicationName("altmiustmu")
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
    {
        o.SignIn.RequireConfirmedEmail = true;
        o.SignIn.RequireConfirmedAccount = true;
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.AllowedForNewUsers = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager<AppSignInManager>()
    .AddClaimsPrincipalFactory<AppClaimsFactory>()
    .AddErrorDescriber<TurkishIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = ".altmiustmu.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.LoginPath = "/hesap/giris";
    o.LogoutPath = "/hesap/cikis";
    o.AccessDeniedPath = "/hesap/erisim-engellendi";
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
    o.SlidingExpiration = true;
});

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = ".altmiustmu.af";
    o.HeaderName = "RequestVerificationToken";
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin));

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin", "Admin");
    o.Conventions.AuthorizeFolder("/Tahminler");
    o.Conventions.AuthorizeFolder("/Panel");
    o.Conventions.AuthorizePage("/Gruplar/Index");
    o.Conventions.AuthorizePage("/Hesap/Ayarlar");
}).AddMvcOptions(o =>
{
    var m = o.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "Bu alan zorunludur.");
    m.SetMissingBindRequiredValueAccessor(name => $"'{name}' alanı zorunludur.");
    m.SetMissingKeyOrValueAccessor(() => "Bu alan zorunludur.");
    m.SetMissingRequestBodyRequiredValueAccessor(() => "İstek gövdesi boş olamaz.");
    m.SetAttemptedValueIsInvalidAccessor((value, _) => $"'{value}' geçerli bir değer değil.");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"'{value}' geçerli bir değer değil.");
    m.SetUnknownValueIsInvalidAccessor(_ => "Girilen değer geçerli değil.");
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Girilen değer geçerli değil.");
    m.SetValueIsInvalidAccessor(value => $"'{value}' geçerli değil.");
    m.SetValueMustBeANumberAccessor(_ => "Bu alan bir sayı olmalı.");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "Bu alan bir sayı olmalı.");
});
builder.Services.Configure<RouteOptions>(o =>
{
    o.LowercaseUrls = true;
    o.LowercaseQueryStrings = false;
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login, sign-up and password reset: 8 POSTs per minute per IP. GETs are never limited.
    o.AddPolicy("auth", ctx => HttpMethods.IsPost(ctx.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("get"));

    // Pick toggles and other HTMX writes: generous, per user.
    o.AddPolicy("writes", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    o.AddPolicy("cron", _ => RateLimitPartition.GetFixedWindowLimiter(
        "cron",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Railway terminates TLS at its edge proxy; trust its X-Forwarded-* headers (the container is not reachable directly).
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 1;
});
builder.Services.AddHttpsRedirection(o => o.HttpsPort = 443);
builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(180));

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// ---------- CLI mode: `dotnet AltMiUstMu.Web.dll sync|migrate|seed|seed-demo` ----------
if (command is not null)
{
    return await CliRunner.RunAsync(app, command);
}

// ---------- Startup migrations (advisory-lock protected) ----------
if (!app.Configuration.GetValue("SKIP_MIGRATIONS", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<MigrationRunner>().MigrateAsync();
}

// ---------- Pipeline ----------
app.UseForwardedHeaders();
app.UseSerilogRequestLogging(o =>
{
    o.GetLevel = (ctx, _, ex) => ex is not null || ctx.Response.StatusCode >= 500
        ? LogEventLevel.Error
        : ctx.Request.Path.StartsWithSegments("/health") || ctx.Request.Path.StartsWithSegments("/lib") || ctx.Request.Path.StartsWithSegments("/css")
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/hata/500");
    app.UseWhen(
        ctx => !ctx.Request.Path.StartsWithSegments("/health") && !ctx.Request.Path.StartsWithSegments("/api"),
        branch =>
        {
            branch.UseHsts();
            branch.UseHttpsRedirection();
        });
}

app.UseStatusCodePagesWithReExecute("/hata/{0}");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkish),
    SupportedCultures = [turkish],
    SupportedUICultures = [turkish],
});

app.Use(async (ctx, next) =>
{
    var headers = ctx.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers.XFrameOptions = "DENY";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers.Vary = "HX-Request";
    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Files referenced with asp-append-version get a ?v= hash, so they can be cached for a long time.
        if (ctx.Context.Request.Query.ContainsKey("v") || ctx.Context.Request.Path.StartsWithSegments("/lib"))
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    },
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapHealthChecks("/health");

app.MapPost("/api/cron/sync", async (HttpContext ctx, SyncService sync, IConfiguration config, CancellationToken ct) =>
    {
        var secret = config["CRON_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Results.NotFound();
        }

        var provided = ctx.Request.Headers["X-Cron-Secret"].ToString();
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (provided.Length == 0 && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            provided = auth["Bearer ".Length..].Trim();
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(secret)))
        {
            return Results.Unauthorized();
        }

        var outcome = await sync.RunAsync(SyncTrigger.Cron, ct);
        return Results.Json(new { success = outcome.Success, message = outcome.Message, runId = outcome.RunId }, statusCode: outcome.Success ? 200 : 500);
    })
    .RequireRateLimiting("cron")
    .DisableAntiforgery();

app.MapRazorPages();

await app.RunAsync();
return 0;
