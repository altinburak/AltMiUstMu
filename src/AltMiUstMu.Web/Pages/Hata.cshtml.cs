using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
            404 => ("Air ball! Sayfa bulunamadı", "Aradığın sayfa yok, taşınmış ya da hiç var olmamış olabilir.", "🏀"),
            403 => ("Bu alana giriş yok", "Bu sayfayı görüntüleme iznin bulunmuyor.", "🚫"),
            401 => ("Giriş yapman gerekiyor", "Devam etmek için hesabına giriş yap.", "🔐"),
            429 => ("Biraz yavaş!", "Kısa sürede çok fazla istek gönderdin. Bir dakika bekleyip tekrar dene.", "⏱️"),
            400 => ("Geçersiz istek", "İstek işlenemedi. Sayfayı yenileyip tekrar dene.", "🤔"),
            _ => ("Teknik faul!", "Beklenmeyen bir hata oluştu. Ekibimiz haberdar edildi, lütfen biraz sonra tekrar dene.", "🛠️"),
        };
        Response.StatusCode = Code;
    }
}
