# DECISIONS.md

Every assumption and judgment call made while building **Alt mı Üst mü?**, grouped by area.

## Product & rules

1. **Current season = the most recently created `Season` row.** There is no "create new season" UI. For 2027-28, run the seed with a new label (or add a row) and enter lines. Groups are tied to the season they were created in.
2. **Picks are locked when `now >= LockAt` OR `Status != Upcoming`.** Both are checked so a stale status or a moved `LockAt` can never quietly reopen picks. To reopen, the admin must set both the status back to *Upcoming* and `LockAt` to the future.
3. **A pick can be switched between ALT/ÜST but not cleared** by a regular user. Once you have 30 picks you stay complete.
4. **"Completed all 30 picks" time = the latest `Pick.CreatedAt`** of that user. Changing a side only updates `UpdatedAt`, so editing a pick never moves you down the tiebreaker. No extra column is needed.
5. **Projection exactly equal to the line is `Pending`** (neutral, counts for nobody). This can only happen with whole-number lines, e.g. 10-10 → 41.0 vs 41.
6. **Whole-number lines:** Over is clinched only when `wins > line`; Under only when `wins + remaining < line`. If neither is decided and no games remain, the result is a **Push**: 0 points, shown as "Push".
7. **Ranking:** primary key is *Projeksiyon* (clinched + trending correct) during the season and *Kesin* (= final correct) once the season is `Finished`. Ties: more clinched wins, then earlier completion. Players identical on all three keys **share a rank** (1, 2, 2, 4).
8. **Pundits are ranked** on the global leaderboard (highlighted with a "🎙️ Mutfak" badge), so everyone can see where Kaan and İnan stand. They need all 30 picks to be ranked, like everyone else.
9. **Disabled users** are excluded from ranking and from community splits, their public profile returns 404, and their sessions are invalidated (security stamp, re-validated every 5 minutes).
10. **The leaderboard before lock** shows an informative empty state (countdown plus the number of players who completed 30 picks) instead of a ranking. Everyone has 0 points before the season, and a ranking by completion time would be meaningless.
11. **Community split ("%64 ÜST") is shown before lock.** It is aggregated and does not reveal anyone's individual picks. It counts regular players only (not pundits, not disabled users).
12. **Admins can see everyone's picks before lock.** Regular users see only their own and the pundits'.
13. **Team status badge names:** Kesin ✓ (clinched win), Gidiyor (trending win), Riskte (trending loss), Kaybetti (clinched loss), Push, Bekliyor (no games / exact projection), Seçilmedi (no pick).
14. **Display names:** 3-20 chars of letters (incl. Turkish), digits, space, `.`, `-`, `_`; at least one letter. Uniqueness is checked on a **folded key** (`İnan Özdemir` = `inan ozdemir` = `INAN OZDEMIR`), which also powers profile URLs (`/oyuncu/{ad}` is case- and Turkish-accent-insensitive) and leaderboard search.
15. **Profanity filter** is a small Turkish+English list. Long words match anywhere, short ambiguous ones (e.g. `GOT`, `MK`, `AMK`) only as whole words, so names like "Göktuğ" pass. Pundit names and "admin"-like names are reserved for regular sign-ups; the admin can still create pundits with any valid name.
16. **Groups:** a group's leaderboard is readable by anyone with its URL (the slug contains a random suffix), so shared links render with OG tags. **Joining requires the separate invite code.** Only members see the invite link. When the owner leaves, ownership passes to the longest-standing member; if nobody is left, the group is deleted. Limits: 10 owned groups, 30 memberships, 500 members per group.
17. **Group rank** follows the global rank order (ties share a rank). Members who are not ranked are listed below. Before lock the table shows each member's pick progress instead of points.
18. **Placeholder data** (in `TeamCatalog`): lines are rough guesses based on 2025-26 results; **LockAt = 2026-10-20 23:00 UTC** (expected opening night). The admin must replace both.

## Results sync

19. **ESPN endpoint is always called with `?season={endYear}&seasontype=2`.** The verified default response (without parameters) returns the *preseason* table (`seasonType: 1`). `endYear` comes from the label (`2026-27` → 2027).
20. **Team mapping:** ESPN team id first (all 30 ids verified against the live API), then abbreviation through an explicit alias table (`NY→NYK, SA→SAS, GS→GSW, NO→NOP, UTAH→UTA, WSH→WAS`).
21. **Validation aborts the whole sync** (nothing written, `SyncRun = Failed`) unless the response has exactly as many teams as the DB (30), every team maps, there are no duplicates, every number is ≥ 0, `W+L ≤ GamesPerTeam`, and no non-manual team's games played went down.
22. **Manual records are skipped**, and the "games decreased" check ignores them (a manual record may legitimately be ahead of or behind ESPN).
23. **If the advisory lock is held, the run is recorded as `Skipped` and the CLI exits 1.** Failing loudly beats silently doing nothing.
24. **Season status auto-advances during sync** (Upcoming → Active after `LockAt`, Active → Finished once every team has played `GamesPerTeam` games) and never moves backwards automatically.
25. **Snapshots:** one `DailySnapshot` per complete player per **Istanbul calendar day**, written by the sync (upserted, so several syncs per day keep the last one). None are written while the season is Upcoming. Admin changes recompute `UserScore` but do not touch snapshots.
26. **Rank movement arrows** compare against the most recent snapshot from an *earlier* day.
27. **Cron schedule `0 9 * * *` (09:00 UTC = 12:00 TSİ):** all US games are final by then.
28. **Polly:** 3 exponential retries with jitter plus a 15-second per-attempt timeout on the ESPN client. Emails are sent **without** retries, because a retried POST could send duplicates.

## Architecture

29. **`AppUser` lives in Infrastructure, not Core,** because it inherits `IdentityUser` (an ASP.NET Identity type). Core entities reference users by `UserId` string only, and EF configures the FKs without navigations. Core has zero package references.
30. **Precomputed scores:** a `UserScore` table (one row per player and season) is rewritten in bulk after every sync and every admin change. Pages only read it. Per-team statuses on pages are evaluated on the fly by the pure engine (30 cheap calls, no user aggregation).
31. **Single transaction** for record updates + season status + score recompute + snapshots + `SyncRun` success row. The `SyncRun` "Running" row is written before the transaction, so a crash leaves an audit trail.
32. **Advisory locks use a dedicated `NpgsqlConnection`** (session-level `pg_try_advisory_lock`), independent of the EF transaction. Migrations use a *blocking* advisory lock so several replicas (web + cron) wait their turn instead of racing. If the database does not exist yet, the lock is skipped and EF creates it.
33. **CLI commands share the web app's DI container.** `Program.cs` detects `sync|migrate|seed|seed-demo` as the first argument, builds the same host without starting Kestrel, runs the command, and returns 0/1. Every command applies pending migrations first, so the cron service also works against a fresh database.
34. **Output caching:** public pages (landing, leaderboard, teams, team, profile) are cached for **anonymous** visitors only. ASP.NET's default policy never caches authenticated requests or responses that set cookies, which is why the antiforgery token is emitted only for signed-in users. Entries vary by query string, by the `HX-Request` header, and by a **data version** (last successful sync + last admin change, re-read at most every 60 s). The in-process sync/admin path also evicts the cache tag immediately. A sync run by the separate cron process is therefore visible within ~60 s, and the hard expiry is 10 minutes.
35. **Validation uses DataAnnotations** (with Turkish messages) plus `NameRules` in Core. FluentValidation was not needed. Model-binding messages are translated too.
36. **Account pages are custom Razor Pages under `/hesap/*`** (equivalent to a fully scaffolded Identity UI, but in Turkish and in the app's design), so the `Microsoft.AspNetCore.Identity.UI` package is not used.
37. **Password policy:** min 8 chars, at least one lowercase letter and one digit (no symbol/uppercase requirement; friendlier on phones). Lockout after 5 failures for 15 minutes. Email confirmation is required to sign in. After confirmation the user is signed in automatically, and an invite `returnUrl` survives the whole sign-up → confirm flow.
38. **Pundits cannot sign in** (custom `SignInManager.CanSignInAsync`, no password, lockout set to max). Their email domain is `.invalid`.
39. **Rate limits (per IP / per user):** auth POSTs (login, sign-up, reset, resend, settings) 8/min; pick and group writes 120/min; cron endpoint 5/min. GET requests to auth pages are not limited. Rejections render the Turkish 429 page (or an htmx toast).
40. **Forwarded headers:** all proxies are trusted (`KnownIPNetworks/KnownProxies` cleared) because Railway's edge is the only way in, with `ForwardLimit = 1` (the right-most, Railway-appended address wins). HTTPS redirection (port 443) and HSTS apply everywhere **except `/health` and `/api/*`**, so Railway's internal health check over plain HTTP is not redirected.
41. **Culture:** requests run in `tr-TR` (Turkish number/date formatting, e.g. "47,5"). Every value written into CSS/JS/JSON uses the invariant culture, all code-level string comparisons are ordinal/invariant (avoiding the Turkish-İ problem), and admin line inputs accept both `47.5` and `47,5`. The HTML encoder allows all Unicode ranges, so ı/ş/ğ are emitted as-is (not as `&#x131;`).
42. **Time zone:** `Europe/Istanbul`, with fallback to the Windows id and finally a fixed UTC+3 (Türkiye has no DST since 2016), so the app works even in images without tzdata. Admin enters `LockAt` in Istanbul time; it is stored as UTC.
43. **Front-end libraries (htmx 2.0.11, Alpine 3.17.4, Chart.js 4.5.1) are vendored** into `wwwroot/lib` rather than loaded from a CDN, so the app has no third-party runtime dependency except Google Fonts (Inter).
44. **Tailwind v4 standalone CLI** (v4.3.3, CSS-first config in `Styles/app.css`). An MSBuild target downloads the binary for the current OS/arch into `/.tools` on first build and rebuilds the CSS incrementally. The generated `wwwroot/css/app.css` is git-ignored. On Railway the same target runs inside `dotnet publish` (the Linux binary is downloaded during the build). `-p:SkipTailwind=true` skips it.
45. **Team badges** are abbreviation tiles in the team's primary colour with a secondary-colour stripe. Text colour (black/white) is picked by WCAG luminance. No logos are used.
46. **Email:** Resend HTTP API when `RESEND_API_KEY` is set; otherwise `LoggingEmailService` logs the full email (including links). Links use `APP_URL` when set (needed behind Railway's proxy and from the CLI), else the current request's host.
47. **Data Protection keys** are stored in Postgres (`DataProtectionKeys` table, application name `altmiustmu`), so redeploys keep users signed in and email/reset tokens valid.
48. **Mobile-first navigation:** bottom tab bar on phones, top nav on desktop. Dark theme by default (`localStorage` remembers the choice, applied before first paint to avoid a flash).
49. **FluentAssertions is pinned to 7.2.x**: v8 changed to a commercial license.
50. **Tests use EF Core InMemory** for service-level tests (lock enforcement, sync flow), with fakes for the provider, the advisory lock, the clock (`TimeProvider`) and the cache. The pure engine/validator/rules are tested directly.

## Demo data (`seed-demo`)

51. **Moves the season's `LockAt` 75 days into the past** and sets it Active, so all in-season views can be checked. It writes Api-sourced mid-season records (~38-44 games), 50 users (`demo01..demo50@demo.altmiustmu.test`, password `Demo1234`), ~90% with complete picks biased toward the "right" side, pundit picks (if empty), 4 groups, and 30 days of synthetic snapshot history. It is idempotent: on a second run it only refreshes records and scores.
52. Because demo records are Api-sourced and mid-season, a real ESPN sync against a demo database will (correctly) **fail validation** ("games played decreased"). That is the safety net working. Use a fresh database for real data.

## Not done / out of scope

53. **No Docker. Railway builds with Railpack** (`railpack.json`). Railpack's built-in .NET provider only detects a `*.csproj` in the repo root and reads the SDK version from `TargetFramework` inside it. Our csproj files live in `src/`, and the framework is set in `Directory.Build.props`, so `railpack.json`:
    - forces the `dotnet` provider and pins `dotnet: 10.0`
    - replaces the root-only restore step
    - publishes `src/AltMiUstMu.Web` to `out/`
    - starts the native apphost `./out/AltMiUstMu.Web`, the same way Railpack's own .NET examples start. The `dotnet` CLI is not on PATH in the runtime image; `DOTNET_ROOT` points the apphost at the runtime.

    The generated plan was checked with the Railpack CLI (v0.40.1). The exact build command was run on a clean copy of the repo (no `.tools`, `bin/obj` or CSS). The resulting apphost then ran `seed`, `sync` and the web server against Postgres in Production mode. The full BuildKit image build itself was not run locally (it needs Docker/BuildKit).
54. **Two Railway config files:** `railway.json` (web: start command, `/health` healthcheck, restart on failure) and `railway.cron.json` (cron: `./out/AltMiUstMu.Web sync`, `0 9 * * *`, restart never). Railway config-as-code overrides dashboard settings, so the cron service must point at its own file. Otherwise it would inherit the web healthcheck and start command.
55. No account deletion / GDPR export UI. An admin can disable a user, and data can be removed in the database if requested.
