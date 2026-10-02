using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using static AltMiUstMu.Web.Localization.Lang;

namespace AltMiUstMu.Web.Pages;

[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class HataModel(ILogger<HataModel> logger) : PageModel
{
    public int Code { get; private set; }
    public string Heading { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string Emoji { get; private set; } = "🏀";

    public void OnGet(int code) => Set(code);

    public void OnPost(int code) => Set(code);

    private void Set(int code)
    {
        Code = code is >= 400 and <= 599 ? code : 500;
        if (Code >= 500 && HttpContext.Features.Get<IExceptionHandlerPathFeature>() is { } failure)
        {
            logger.LogError(failure.Error, "Unhandled exception on {Path}", failure.Path);
        }

        (Heading, Message, Emoji) = Code switch
        {
            404 => (T("Air ball! Sayfa bulunamadı", "Air ball! Page not found"), T("Aradığın sayfa yok, taşınmış ya da hiç var olmamış olabilir.", "The page you're looking for doesn't exist, has moved, or never existed."), "🏀"),
            403 => (T("Bu alana giriş yok", "No entry here"), T("Bu sayfayı görüntüleme iznin bulunmuyor.", "You don't have permission to view this page."), "🚫"),
            401 => (T("Giriş yapman gerekiyor", "You need to sign in"), T("Devam etmek için hesabına giriş yap.", "Sign in to your account to continue."), "🔐"),
            429 => (T("Biraz yavaş!", "Slow down!"), T("Kısa sürede çok fazla istek gönderdin. Bir dakika bekleyip tekrar dene.", "You sent too many requests in a short time. Wait a minute and try again."), "⏱️"),
            400 => (T("Geçersiz istek", "Bad request"), T("İstek işlenemedi. Sayfayı yenileyip tekrar dene.", "The request couldn't be processed. Refresh the page and try again."), "🤔"),
            _ => (T("Teknik faul!", "Technical foul!"), T("Beklenmeyen bir hata oluştu. Ekibimiz haberdar edildi, lütfen biraz sonra tekrar dene.", "Something unexpected went wrong. We've been notified, please try again in a bit."), "🛠️"),
        };
        Response.StatusCode = Code;
    }
}
