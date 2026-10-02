using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltMiUstMu.Web.Pages.Admin;

public class DenetimModel(AppDbContext db) : AdminPageModel
{
    public const int PageSize = 100;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public List<AdminAuditLog> Rows { get; private set; } = [];
    public int Total { get; private set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task OnGetAsync(CancellationToken ct)
    {
        Total = await db.AdminAuditLogs.CountAsync(ct);
        PageNumber = Math.Clamp(PageNumber, 1, PageCount);
        Rows = await db.AdminAuditLogs.AsNoTracking().OrderByDescending(a => a.Id)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync(ct);
    }
}
