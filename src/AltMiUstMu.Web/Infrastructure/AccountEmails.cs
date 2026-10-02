using System.Text.Encodings.Web;
using AltMiUstMu.Core.Abstractions;
using AltMiUstMu.Web.Localization;

namespace AltMiUstMu.Web.Infrastructure;

/// <summary>Builds absolute links (APP_URL when configured, otherwise the current request) and sends emails in the recipient's language.</summary>
public sealed class AccountEmails(IEmailService email, IConfiguration config, IHttpContextAccessor http)
{
    public const string AppName = "Alt mı Üst mü?";

    public string BaseUrl
    {
        get
        {
            var configured = config["APP_URL"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.TrimEnd('/');
            }

            var request = http.HttpContext?.Request;
            return request is null ? "http://localhost:8080" : $"{request.Scheme}://{request.Host}{request.PathBase}";
        }
    }

    public string Absolute(string relative) => BaseUrl + (relative.StartsWith('/') ? relative : "/" + relative);

    public Task SendConfirmationAsync(string to, string displayName, string relativeLink, string language)
    {
        var en = Lang.Normalize(language) == Lang.English;
        var name = HtmlEncoder.Default.Encode(displayName);
        return email.SendAsync(
            to,
            en ? $"{AppName} – Verify your email address" : $"{AppName} – E-posta adresini doğrula",
            Layout(
                en,
                en ? $"Welcome {name}!" : $"Hoş geldin {name}!",
                en
                    ? "To activate your account, please verify your email address. Then you can make your UNDER or OVER picks for all 30 teams."
                    : "Hesabını etkinleştirmek için e-posta adresini doğrulaman gerekiyor. Sonra 30 takım için ALT mı ÜST mü, tahminlerini yapabilirsin.",
                en ? "Verify my email" : "E-postamı doğrula",
                Absolute(relativeLink)));
    }

    public Task SendPasswordResetAsync(string to, string relativeLink, string language)
    {
        var en = Lang.Normalize(language) == Lang.English;
        return email.SendAsync(
            to,
            en ? $"{AppName} – Password reset" : $"{AppName} – Şifre sıfırlama",
            Layout(
                en,
                en ? "Forgot your password?" : "Şifreni mi unuttun?",
                en
                    ? "Click the button below to reset your password. If you didn't request this, you can ignore this email."
                    : "Şifreni sıfırlamak için aşağıdaki butona tıkla. Bu isteği sen yapmadıysan bu e-postayı görmezden gelebilirsin.",
                en ? "Set a new password" : "Yeni şifre belirle",
                Absolute(relativeLink)));
    }

    private static string Layout(bool en, string title, string body, string cta, string url)
    {
        var safeUrl = HtmlEncoder.Default.Encode(url);
        var lang = en ? "en" : "tr";
        var fallback = en ? "If the button doesn't work, paste this link into your browser:" : "Buton çalışmazsa bu bağlantıyı tarayıcına yapıştır:";
        var footer = en ? "A completely free prediction game, just for fun. No money, no betting." : "Tamamen ücretsiz, eğlence amaçlı bir tahmin oyunu. Para yok, bahis yok.";
        return $$"""
        <!doctype html>
        <html lang="{{lang}}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{AppName}}</title></head>
        <body style="margin:0;background:#07090d;font-family:Inter,Segoe UI,Roboto,Arial,sans-serif;color:#e4e4e7">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#07090d;padding:32px 12px">
            <tr><td align="center">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#0d1117;border:1px solid #263142;border-radius:16px">
                <tr><td style="padding:28px 28px 8px">
                  <div style="font-size:20px;font-weight:800;color:#ffffff">🏀 Alt mı <span style="color:#f97316">Üst</span> mü?</div>
                </td></tr>
                <tr><td style="padding:8px 28px 0">
                  <h1 style="font-size:22px;line-height:1.3;color:#ffffff;margin:12px 0">{{title}}</h1>
                  <p style="font-size:15px;line-height:1.6;color:#a1a1aa;margin:0 0 24px">{{body}}</p>
                  <a href="{{safeUrl}}" style="display:inline-block;background:#f97316;color:#ffffff;text-decoration:none;font-weight:700;padding:12px 22px;border-radius:12px">{{cta}}</a>
                  <p style="font-size:12px;line-height:1.6;color:#71717a;margin:24px 0 0">{{fallback}}<br><span style="word-break:break-all;color:#a1a1aa">{{safeUrl}}</span></p>
                </td></tr>
                <tr><td style="padding:24px 28px 28px;font-size:12px;color:#52525b">{{footer}}</td></tr>
              </table>
            </td></tr>
          </table>
        </body></html>
        """;
    }
}
