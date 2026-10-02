using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AltMiUstMu.Infrastructure.Data;

public static class ConnectionStringResolver
{
    /// <summary>
    /// DATABASE_URL (Railway style postgres://user:pass@host:port/db, or a plain Npgsql connection string)
    /// wins over ConnectionStrings:Default.
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        var url = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url))
        {
            return Normalize(url);
        }

        var cs = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(cs))
        {
            return cs;
        }

        throw new InvalidOperationException(
            "Veritabanı bağlantısı bulunamadı. DATABASE_URL ortam değişkenini veya ConnectionStrings:Default ayarını tanımlayın.");
    }

    public static string Normalize(string value)
    {
        value = value.Trim();
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            SslMode = SslMode.Prefer,
        };

        // Honor ?sslmode=require etc. if present in the URL.
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(kv[1].Replace("-", "", StringComparison.Ordinal), ignoreCase: true, out var mode))
            {
                builder.SslMode = mode;
            }
        }

        return builder.ConnectionString;
    }
}
