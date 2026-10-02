using AltMiUstMu.Core.Text;
using AltMiUstMu.Infrastructure.Data;
using AltMiUstMu.Infrastructure.Identity;
using AltMiUstMu.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Admin;

public sealed record AdminUserRow(string Id, string DisplayName, string Email, DateTime CreatedAt, bool EmailConfirmed, bool IsDisabled, bool IsAdmin, int Picks);

public class KullanicilarModel(AppDbContext db, SeasonService seasons, AdminService admin) : AdminPageModel
{
    public const int PageSize = 50;

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public List<AdminUserRow> Rows { get; private set; } = [];
    public int Total { get; private set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task OnGetAsync(CancellationToken ct)
    {
        var season = await seasons.GetCurrentAsync(ct);
        var seasonId = season?.Id ?? 0;
        var adminRoleId = await db.Roles.Where(r => r.Name == Roles.Admin).Select(r => r.Id).SingleOrDefaultAsync(ct);

        var users = db.Users.AsNoTracking().Where(u => !u.IsPundit);
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var key = TextNormalizer.Fold(Query);
            var email = Query.Trim().ToUpperInvariant();
            users = users.Where(u => u.DisplayNameKey.Contains(key) || u.NormalizedEmail!.Contains(email));
        }

        Total = await users.CountAsync(ct);
        PageNumber = Math.Clamp(PageNumber, 1, PageCount);
        Rows = await users.OrderByDescending(u => u.CreatedAt)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize)
            .Select(u => new AdminUserRow(
                u.Id,
                u.DisplayName,
                u.Email ?? "",
                u.CreatedAt,
                u.EmailConfirmed,
                u.IsDisabled,
                db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == adminRoleId),
                db.Picks.Count(p => p.UserId == u.Id && p.SeasonId == seasonId)))
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostToggleAsync(string userId, bool disable, CancellationToken ct)
    {
        var error = await admin.SetDisabledAsync(Actor, userId, disable, ct);
        if (error is null)
        {
            Flash(disable ? "Kullanıcı devre dışı bırakıldı." : "Kullanıcı yeniden etkinleştirildi.");
        }
        else
        {
            FlashError(error);
        }

        return RedirectToPage(new { q = Query, p = PageNumber });
    }
}
