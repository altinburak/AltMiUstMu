# 🏀 Alt mı Üst mü?

**Alt mı Üst mü?**, NBA sezonu için ücretsiz bir galibiyet tahmin oyunu. Para yok, bahis yok, sadece eğlence.

Sezon başlamadan önce yönetici 30 NBA takımının Vegas galibiyet baremlerini girer. Oyuncular e-postayla kayıt olur ve her takım için **ALT** ya da **ÜST** der. Tahminler sezon başlayınca kilitlenir. Gerçek maç sonuçları her gün otomatik olarak çekilir; puanlar, sıralama ve grafikler güncellenir.

Oyun, Türkçe NBA programı **Amerikan Mutfak**'tan ilham alıyor. Sunucular **Kaan Kural** ve **İnan Özdemir**'in tahminleri de oyunda: *Kaan ve İnan'ı geçebilecek misin?*

---

## İçindekiler

- [Özellikler](#özellikler)
- [Teknoloji](#teknoloji)
- [Proje yapısı](#proje-yapısı)
- [Yerelde çalıştırma](#yerelde-çalıştırma)
- [Komutlar](#komutlar)
- [Ortam değişkenleri](#ortam-değişkenleri)
- [Railway'e kurulum (adım adım)](#railwaye-kurulum-adım-adım)
- [Sezon yönetimi](#sezon-yönetimi)
- [Puanlama kuralları](#puanlama-kuralları)
- [Testler](#testler)

---

## Özellikler

| Sayfa | Adres | Açıklama |
|---|---|---|
| Ana sayfa | `/` | Kilit geri sayımı (sezon öncesi) ya da ilk 5 ve Mutfak puanları (sezon içinde) |
| Tahminlerim | `/tahminler` | 30 takım kartı, büyük ALT/ÜST butonları, anında kayıt (HTMX), yapışkan ilerleme çubuğu, "Kaan ve İnan ne dedi?" anahtarı |
| Panel | `/panel` | Sıran, projeksiyon/kesin puan, sıralama grafiği, takım takım durum, Kaan ve İnan'la karşılaştırma |
| Liderlik | `/liderlik` | Arama ve HTMX ile sayfalama, Mutfak rozeti, kendi satırın sabit, dünden bugüne değişim okları |
| Gruplar | `/gruplar` | Grup kur, davet bağlantısıyla katıl (`/gruplar/katil/{kod}`), grup sıralaması (istersen Mutfak dahil) |
| Takım | `/takim/{kısaltma}` | Barem, derece, projeksiyon, topluluk dağılımı, Mutfak tahminleri |
| Takımlar | `/takimlar` | Sıralanabilir tablo |
| Profil | `/oyuncu/{kullanıcı adı}` | Puan, sıralama geçmişi, tahminler (kilitten sonra) |
| Yönetim | `/admin` | Sezon ayarları, 30 baremi tek formda düzenleme, Mutfak tahminleri, manuel derece, senkronizasyon, kullanıcılar, denetim kaydı |

Diğer özellikler:

- Tamamen Türkçe arayüz, e-postalar ve hata mesajları. Saatler İstanbul saatiyle gösterilir, veritabanında UTC tutulur.
- Mobil öncelikli tasarım: koyu tema varsayılan, açık tema seçeneği var.
- WhatsApp ve Twitter paylaşımları için Open Graph etiketleri.
- Takım logoları kullanılmaz (marka hakları). Takımlar, takım renklerinde kısaltma rozetleriyle gösterilir.

## Teknoloji

- .NET 10, ASP.NET Core Razor Pages, C# (nullable açık, uyarılar hata sayılır)
- HTMX ve Alpine.js (`wwwroot/lib` içinde, CDN gerekmez), Chart.js
- Tailwind CSS v4, standalone CLI ile derleme anında üretilir (Node.js gerekmez)
- EF Core 10 + Npgsql (PostgreSQL), code-first migration'lar
- ASP.NET Core Identity: e-posta doğrulama, şifre sıfırlama, hesap kilitleme, Türkçe hata mesajları
- Resend HTTP API ile e-posta (anahtar yoksa linkler konsola yazılır)
- Serilog (konsol), rate limiting, output caching, health check
- xUnit + FluentAssertions

## Proje yapısı

```
src/
  AltMiUstMu.Core/            Varlıklar, saf puanlama motoru (ScoringEngine), kurallar, arayüzler (bağımlılık yok)
  AltMiUstMu.Infrastructure/  EF Core DbContext + migration'lar, ESPN sağlayıcısı, e-posta, sync/puan/seed servisleri
  AltMiUstMu.Web/             Razor Pages, HTMX partial'ları, Identity sayfaları, admin, CLI komutları
tests/
  AltMiUstMu.Tests/           Puanlama, sync doğrulama, kilit, servis testleri
railpack.json                 Railway'in derleme tarifi (Railpack, Docker gerekmez)
railway.json                  Web servisinin Railway ayarları (başlatma komutu, sağlık kontrolü)
railway.cron.json             Günlük senkronizasyon servisinin Railway ayarları (cron)
DECISIONS.md                  Verilen tüm kararlar ve varsayımlar
```

## Yerelde çalıştırma

**Gerekenler:** [.NET 10 SDK](https://dotnet.microsoft.com/download) ve [PostgreSQL](https://www.postgresql.org/download/) (14 veya üzeri). Docker ve Node.js gerekmez.

1. **Veritabanı kullanıcısını oluştur.** `psql`'i postgres kullanıcısıyla aç ve şunu çalıştır:
   ```sql
   CREATE USER altmiustmu WITH PASSWORD 'altmiustmu' CREATEDB;
   ```
   `appsettings.Development.json` bu bilgilere göre ayarlı. Veritabanını uygulama ilk açılışta kendisi oluşturur.
   Farklı bir kullanıcı ya da şifre kullanacaksan `DATABASE_URL` ortam değişkenini ayarla:
   ```bash
   export DATABASE_URL="postgres://kullanici:sifre@localhost:5432/altmiustmu"   # PowerShell: $env:DATABASE_URL="..."
   ```

2. **Derle.** İlk derlemede Tailwind CLI otomatik indirilir ve `/.tools` klasörüne kaydedilir.
   ```bash
   dotnet build
   ```

3. **Temel verileri yükle:** 30 takım, 2026-27 sezonu, örnek baremler, Kaan ve İnan, admin kullanıcısı.
   ```bash
   dotnet run --project src/AltMiUstMu.Web -- seed
   ```
   Geliştirme ortamında admin girişi: `admin@altmiustmu.local` / `Admin12345`.

4. **(İsteğe bağlı) Demo verisi:** 50 sahte oyuncu, gruplar, sezon ortası dereceler ve 30 günlük grafik geçmişi.
   ```bash
   dotnet run --project src/AltMiUstMu.Web -- seed-demo
   ```
   Demo hesapları `demo01@demo.altmiustmu.test` ile `demo50@demo.altmiustmu.test` arasında, şifre `Demo1234`.
   ⚠️ Bu komut sezonun kilit tarihini geçmişe çeker; sadece geliştirme ortamı içindir (Production'da çalışmaz).

5. **Uygulamayı başlat**
   ```bash
   dotnet run --project src/AltMiUstMu.Web
   ```
   → http://localhost:5080

   Varsayılan olarak e-posta doğrulaması kapalı: kayıt olan kullanıcı hemen giriş yapmış olur (bkz. `REQUIRE_EMAIL_CONFIRMATION`).

**CSS'i canlı izlemek için:** `./scripts/tailwind-watch.sh` (Windows'ta `./scripts/tailwind-watch.ps1`).

## Komutlar

Aynı uygulama hem web sunucusu hem de tek seferlik komut olarak çalışır. Her komut önce eksik migration'ları uygular. Başarılı olursa `0`, başarısız olursa `1` koduyla çıkar.

- **Yerelde:** `dotnet run --project src/AltMiUstMu.Web -- <komut>`
- **Railway'de:** `./out/AltMiUstMu.Web <komut>`

| Komut | Ne yapar |
|---|---|
| *(komutsuz)* | Web sunucusu (`PORT` değişkenindeki portu dinler, açılışta migration uygular) |
| `migrate` | Sadece migration'ları uygular |
| `seed` | Takımlar, sezon, örnek baremler, Mutfak oyuncuları ve admin. İdempotenttir, tekrar çalıştırmak güvenli |
| `seed-demo` | Demo verisi (`ASPNETCORE_ENVIRONMENT=Production` iken çalışmaz) |
| `sync` | ESPN'den dereceleri çeker, doğrular, puanları ve günlük snapshot'ları yazar |

Senkronizasyon üç yoldan tetiklenebilir. Üçü de aynı servisi kullanır ve bir Postgres advisory lock sayesinde aynı anda iki senkronizasyon çalışamaz:

1. CLI komutu `sync` (Railway Cron bunu kullanır)
2. `POST /api/cron/sync` (header: `X-Cron-Secret: <CRON_SECRET>`)
3. Admin panelindeki **"Şimdi senkronize et"** butonu

## Ortam değişkenleri

Tamamı `.env.example` dosyasında açıklamalarıyla birlikte duruyor.

| Değişken | Zorunlu | Açıklama |
|---|---|---|
| `DATABASE_URL` | ✅ | `postgres://kullanıcı:şifre@host:port/db` ya da Npgsql bağlantı cümlesi. Railway otomatik verir |
| `APP_URL` | ✅ (prod) | Herkese açık adres, örn. `https://altmiustmu.up.railway.app`. E-posta linkleri ve OG etiketleri için |
| `REQUIRE_EMAIL_CONFIRMATION` | ➖ | Varsayılan `false`: e-posta doğrulaması ve e-postayla şifre sıfırlama kapalı, kayıt olan hemen giriş yapar. Şifresini unutanlara admin `/admin/kullanicilar` sayfasından geçici şifre verir. `true` yapacaksan Resend ayarları da gerekir |
| `RESEND_API_KEY` | ➖ | Resend API anahtarı (sadece doğrulama açıkken). Boşsa e-posta gönderilmez, linkler loglanır |
| `EMAIL_FROM` | ➖ | Gönderen, örn. `Alt mı Üst mü? <bildirim@alanadin.com>` (alan adı Resend'de doğrulanmış olmalı) |
| `CRON_SECRET` | ➖ | `/api/cron/sync` için gizli anahtar. Boşsa uç nokta kapalıdır |
| `ADMIN_EMAIL` | ✅ (seed) | `seed` komutunun oluşturacağı admin |
| `ADMIN_PASSWORD` | ✅ (seed) | En az 8 karakter, bir küçük harf ve bir rakam |
| `PORT` | ➖ | Railway verir. Uygulama `0.0.0.0:$PORT` adresini dinler |

## Railway'e kurulum (adım adım)

Railway bu projeyi **Docker kullanmadan**, kendi derleyicisi **Railpack** ile derler. Gereken ayarlar repoda hazır:

| Dosya | Ne işe yarar |
|---|---|
| `railpack.json` | .NET 10 SDK'yı kurar, `dotnet publish` ile derler (Tailwind CSS de bu sırada üretilir) ve çıktıyı `out/` klasörüne koyar |
| `railway.json` | **web** servisi: `./out/AltMiUstMu.Web` ile başlar, sağlık kontrolü `/health` |
| `railway.cron.json` | **sync-cron** servisi: her gün 09:00 UTC'de `./out/AltMiUstMu.Web sync` çalıştırır ve kapanır |

Sonunda projede 3 servis olacak: **Postgres**, **web** ve **sync-cron**.

### 1. Kodu GitHub'a gönder

```bash
git add .
git commit -m "Alt mı Üst mü?"
git push
```

### 2. Proje ve veritabanı

1. [railway.com](https://railway.com) → **New Project** → **Deploy from GitHub repo** → bu repoyu seç.
2. Oluşan servise tıkla → **Settings** → servis adını **web** yap.
3. Proje ekranında **+ Create** → **Database** → **Add PostgreSQL**. Servisin adı **Postgres** olarak kalsın; aşağıdaki değişken referansları bu ada göre yazıldı.

> İlk deploy, değişkenler henüz tanımlı olmadığı için hata verebilir. Sorun değil; 3. adımdan sonra yeniden deploy edeceğiz.

### 3. Değişkenler

Proje ekranında **Settings** → **Shared Variables** bölümüne şunları ekle:

| Değişken | Değer | Not |
|---|---|---|
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` | Aynen böyle yaz. Railway iç ağ adresini kendisi koyar |
| `APP_URL` | `https://<web-alan-adın>` | 4. adımda alacağın adres, sonda `/` olmadan |
| `ADMIN_EMAIL` | `sen@ornek.com` | Admin hesabın |
| `ADMIN_PASSWORD` | güçlü bir şifre | En az 8 karakter, en az bir küçük harf ve bir rakam |
| `CRON_SECRET` | uzun rastgele bir metin | Örn. `openssl rand -hex 32` çıktısı |
| `RESEND_API_KEY` | `re_...` | İsteğe bağlı. Yoksa e-postalar gönderilmez, sadece loglanır |
| `EMAIL_FROM` | `Alt mı Üst mü? <bildirim@alanadin.com>` | Resend'de doğrulanmış bir alan adından olmalı |

Sonra her değişkenin yanındaki **Share** ile değişkenleri **web** servisine (ve 6. adımda oluşturacağın **sync-cron** servisine) bağla. İstersen aynı değişkenleri her servisin kendi **Variables** sekmesine tek tek de ekleyebilirsin.

Şunları **ekleme**, Railway ve Railpack kendisi ayarlıyor: `PORT`, `ASPNETCORE_ENVIRONMENT` (=Production), `ASPNETCORE_CONTENTROOT`, `DOTNET_ROOT`.

### 4. Web servisi: alan adı ve deploy

1. **web** → **Settings** → **Networking** → **Generate Domain**. Port sorulursa **8080** gir. Uygulama, Railway'in verdiği `PORT` değişkenini dinler. İstersen burada kendi alan adını da bağlayabilirsin.
2. Aldığın adresi (`https://xxx.up.railway.app`) `APP_URL` değişkenine yaz.
3. **web** → **Settings** → **Build** bölümünde **Builder: Railpack** seçili olsun. **Root Directory** boş kalsın. `railway.json` bunları zaten belirliyor.
4. **Deploy** et. Değişkenleri kaydedince Railway genelde kendisi deploy eder; etmezse sağ üstten **Deploy**'a bas.
   - Derleme loglarında sırasıyla `install mise packages: dotnet`, `Downloading Tailwind CSS` ve `dotnet publish` adımlarını görürsün.
   - Uygulama açılışta veritabanı tablolarını kendisi oluşturur (migration).
   - `/health` yeşil olunca deploy tamamlanır.

### 5. Seed (sadece bir kere)

Bu adım takımları, sezonu, örnek baremleri, Kaan ve İnan'ı ve admin hesabını oluşturur. İki yoldan birini kullan.

**A) Railway CLI ile, sunucunun içinde (önerilen)**
```bash
npm i -g @railway/cli          # ya da: brew install railway / scoop install railway
railway login
railway link                   # projeyi ve "web" servisini seç
railway ssh                    # web sunucusuna bağlanır
./out/AltMiUstMu.Web seed      # sunucunun içinde çalıştır, sonra: exit
```

**B) Kendi bilgisayarından**

Postgres servisi → **Variables** → `DATABASE_PUBLIC_URL` değerini kopyala ve çalıştır:
```bash
DATABASE_URL="<DATABASE_PUBLIC_URL>" ADMIN_EMAIL="sen@ornek.com" ADMIN_PASSWORD="..." \
  ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/AltMiUstMu.Web -- seed
```

Sonra `https://<alan-adın>/hesap/giris` adresinden admin hesabınla giriş yap. Sağ üstteki menüde **Yönetim** bağlantısı görünür.

### 6. Günlük senkronizasyon servisi (sync-cron)

1. Proje ekranında **+ Create** → **GitHub Repo** → **aynı repoyu** seç. Yeni servisin adını **sync-cron** yap.
2. **sync-cron** → **Settings** → **Config-as-code** → **Railway Config File** alanına `/railway.cron.json` yaz. Bu dosya üç şeyi ayarlar:
   - başlatma komutu: `./out/AltMiUstMu.Web sync`
   - cron zamanlaması: `0 9 * * *`, yani her gün 12:00 TSİ (bu saatte ABD'deki tüm maçlar bitmiş olur)
   - yeniden başlatma politikası: `NEVER`
3. **sync-cron** → **Variables**: 3. adımdaki paylaşılan değişkenleri bu servise de bağla. En az `DATABASE_URL` ve `APP_URL` gerekli.
4. **sync-cron** için alan adı **oluşturma**. Bu servis web trafiği almaz; işini yapar ve kapanır.
5. Deploy et ve **Settings** → **Deploy** altında şunları kontrol et:
   - **Cron Schedule:** `0 9 * * *`
   - **Custom Start Command:** `./out/AltMiUstMu.Web sync`

Her çalıştırmanın sonucu admin panelindeki **Senkronizasyon geçmişi** tablosunda görünür. Komut başarılıysa `0`, değilse `1` koduyla çıkar.

> **Config-as-code alanını bulamazsan:** **Settings** → **Deploy** altında elle gir: **Custom Start Command** `./out/AltMiUstMu.Web sync`, **Cron Schedule** `0 9 * * *`, **Healthcheck Path** boş, **Restart Policy** `Never`.

Senkronizasyonu istersen dışarıdan da tetikleyebilirsin:
```bash
curl -X POST https://<alan-adın>/api/cron/sync -H "X-Cron-Secret: <CRON_SECRET>"
```

### 7. E-posta (isteğe bağlı, varsayılan olarak kapalı)

Varsayılan ayarda uygulama hiç e-posta göndermez. Kayıt olan kullanıcı hemen giriş yapar. Şifresini unutan kullanıcıya admin, **Yönetim → Kullanıcılar → Şifre sıfırla** ile geçici bir şifre verir. Bu adımı atlayabilirsin.

İleride e-posta doğrulamasını açmak istersen:

1. [resend.com](https://resend.com) → **Domains**: alan adını ekle, verilen DNS kayıtlarını gir ve doğrulanmasını bekle.
2. **API Keys** → yeni anahtar oluştur ve `RESEND_API_KEY` değişkenine yaz. `EMAIL_FROM`'u doğruladığın alan adından bir adresle ayarla.
3. **web** servisine `REQUIRE_EMAIL_CONFIRMATION=true` ekle ve yeniden deploy et. Kayıt ve şifre sıfırlama e-postaları artık gerçekten gönderilir.

### 8. Sezonu hazırla

1. `/admin/sezon`: **kilit zamanını** gerçek açılış maçının saatine ayarla (İstanbul saatiyle).
2. `/admin/baremler`: 30 takımın gerçek Vegas baremlerini gir.
3. `/admin/punditler`: Kaan Kural ve İnan Özdemir'in 30 tahminini gir.
4. `/admin`: **Şimdi senkronize et** butonuyla bir kez çalıştır ve sonucun başarılı olduğunu gör.

### Sorun giderme

| Belirti | Çözüm |
|---|---|
| Derleme "could not determine how to build the app" diyor | `railpack.json` repo kökünde olmalı ve servisin **Root Directory** ayarı boş kalmalı |
| Uygulama "Veritabanı bağlantısı bulunamadı" diyerek kapanıyor | `DATABASE_URL` servise bağlı değil. Değişkeni ekleyip yeniden deploy et |
| Deploy sağlık kontrolünde takılıyor | **Deploy Logs**'a bak; genelde veritabanı değişkeni eksiktir. Healthcheck path `/health` olmalı |
| E-postadaki linkler yanlış adrese gidiyor | `APP_URL`'i gerçek alan adına göre düzelt (sonda `/` olmadan) |
| Senkronizasyon "games played decreased" hatası veriyor | Veritabanında demo verisi var. Production'da `seed-demo` çalıştırma |
| Her deploy'da kullanıcıların oturumu kapanıyor | Olmamalı, çünkü anahtarlar veritabanında tutuluyor. `DATABASE_URL`'in her deploy'da aynı veritabanını gösterdiğinden emin ol |

## Sezon yönetimi

`/admin` altındaki adımlar:

1. **Sezon** (`/admin/sezon`): sezon adı, **kilit zamanı** (İstanbul saatiyle; açılış maçının başlama saati), takım başına maç sayısı ve durum. ⚠️ Seed'deki kilit zamanı tahminidir (20 Ekim 2026, 23:00 UTC). Gerçek açılış saatini mutlaka gir.
2. **Baremler** (`/admin/baremler`): 30 takımın Vegas galibiyet baremleri tek formda. `47.5` ya da `47,5` yazabilirsin. Seed'deki değerler **örnektir**, gerçekleriyle değiştir.
3. **Mutfak** (`/admin/punditler`): Kaan Kural ve İnan Özdemir'in 30 tahminini tabloya gir. Kilitten sonra da düzenlenebilir, ama her değişiklik **denetim kaydına** yazılır. Buradan yeni Mutfak oyuncusu da ekleyebilirsin.
4. **Dereceler** (`/admin/kayitlar`): ESPN yanlış veri verirse takımın derecesini elle düzelt. Manuel derece, sen *override'ı kaldırana* kadar senkronizasyon tarafından ezilmez.
5. **Genel** (`/admin`): **Şimdi senkronize et** butonu ve senkronizasyon geçmişi.
6. **Kullanıcılar**: arama ve kullanıcıyı devre dışı bırakma (oturumu kapanır, sıralamadan çıkar).
7. **Denetim**: barem, Mutfak tahmini, manuel derece, sezon ve kullanıcı değişikliklerinin tamamı.

Sezon durumu kendiliğinden ilerler: kilit zamanı geçince **Devam ediyor**, tüm takımlar sezonu bitirince **Bitti**.

## Puanlama kuralları

- Her doğru tahmin 1 puan. Barem tam sayıysa ve takım tam o kadar galibiyette kalırsa sonuç **push** olur (0 puan).
- **Kesin:** matematiksel olarak kesinleşen doğru tahminler.
  - ÜST, galibiyet sayısı baremi geçince kesinleşir.
  - ALT, galibiyet + kalan maç sayısı baremin altına düşünce kesinleşir.
- **Projeksiyon:** kesin doğrular + "gidiyor" olanlar. Mevcut tempoyla hesaplanır: `galibiyet / oynanan maç × 82`.
- Sezon boyunca sıralama projeksiyona, sezon bitince kesin doğru sayısına göre yapılır. Eşitlikte önce daha çok kesin doğrusu olan, sonra 30 tahminini daha erken tamamlayan öne geçer.
- Sıralamaya yalnızca **30 takımın hepsini** seçenler girer.
- Sezon başlayana kadar başkalarının tahminleri gizlidir. Mutfak tahminleri her zaman açıktır.

Puanlar her istekte yeniden hesaplanmaz. Her senkronizasyondan ve her admin değişikliğinden sonra toplu olarak hesaplanıp `UserScores` tablosuna yazılır.

## Testler

```bash
dotnet test
```

Testler puanlama motorunu (kesinleşme, projeksiyon, push, eşitlik bozma, hiç maç oynanmamış durum, sezon sonu), sync doğrulamasını, ESPN eşlemesini, kilit kurallarını ve sunucu tarafı kilit kontrolünü, sync akışını (geçersiz veri yazılmaz, manuel kayıtlar atlanır, sezon otomatik biter, kilit doluysa çalıştırma atlanır) ve `DATABASE_URL` dönüşümünü kapsar.

---

Bu proje NBA ya da takımlarıyla resmi olarak bağlantılı değildir. Amerikan Mutfak'tan ilham alan bir hayran projesidir.
