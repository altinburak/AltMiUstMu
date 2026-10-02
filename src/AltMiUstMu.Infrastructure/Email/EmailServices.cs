using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using AltMiUstMu.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AltMiUstMu.Infrastructure.Email;

public sealed class EmailOptions
{
    public string? ResendApiKey { get; set; }
    public string From { get; set; } = "Alt mı Üst mü? <onboarding@resend.dev>";
}

/// <summary>Sends mail through Resend's HTTP API (https://resend.com/docs/api-reference/emails/send-email).</summary>
public sealed class ResendEmailService(HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailService> logger) : IEmailService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task SendAsync(string to, string subject, string html, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new { from = options.Value.From, to = new[] { to }, subject, html }, options: Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ResendApiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend rejected email to {To}: {Status} {Body}", to, (int)response.StatusCode, body);
            throw new InvalidOperationException("E-posta gönderilemedi.");
        }

        logger.LogInformation("Email '{Subject}' sent to {To}", subject, to);
    }
}

/// <summary>Development fallback: writes the email (and therefore its links) to the log.</summary>
public sealed class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string html, CancellationToken ct = default)
    {
        logger.LogWarning("RESEND_API_KEY not set; email NOT sent. To: {To} | Subject: {Subject}\n{Html}", to, subject, html);
        return Task.CompletedTask;
    }
}
