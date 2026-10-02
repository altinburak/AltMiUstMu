using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AltMiUstMu.Web.Pages.Admin;

public class PunditlerModel(SeasonService seasons, GameQueries queries, PickService picks, AdminService admin) : AdminPageModel
{
    [BindProperty(SupportsGet = true, Name = "id")]
    public string? SelectedId { get; set; }

    /// <summary>Team id -> "Over" | "Under" | "" (cleared).</summary>
    [BindProperty]
    public Dictionary<int, string> Picks { get; set; } = [];

    [BindProperty]
    public string? NewName { get; set; }

    public Season? Season { get; private set; }
    public bool Locked { get; private set; }
    public List<PunditView> Pundits { get; private set; } = [];
    public PunditView? Selected { get; private set; }
    public List<TeamOverview> Teams { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await LoadAsync(ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (!await LoadAsync(ct) || Selected is null)
        {
            return NotFound();
        }

        var changes = Teams.ToDictionary(
            t => t.Id,
            t => Picks.GetValueOrDefault(t.Id) switch
            {
                "Over" => (PickSide?)PickSide.Over,
                "Under" => PickSide.Under,
                _ => null,
            });

        var count = await picks.SetPunditPicksAsync(Actor, Selected.UserId, changes, ct);
        Flash(count == 0 ? "Değişiklik yok." : $"{Selected.DisplayName}: {count} tahmin güncellendi (denetim kaydına yazıldı).");
        return RedirectToPage(new { id = Selected.UserId });
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        var error = await admin.CreatePunditAsync(Actor, NewName ?? "", ct);
        if (error is not null)
        {
            FlashError(error);
        }
        else
        {
            Flash("Yeni Mutfak oyuncusu eklendi.");
        }

        return RedirectToPage();
    }

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return false;
        }

        Locked = seasons.IsLocked(Season);
        Pundits = await queries.GetPunditsAsync(Season.Id, ct);
        Selected = Pundits.FirstOrDefault(p => p.UserId == SelectedId) ?? Pundits.FirstOrDefault();
        Teams = (await queries.GetTeamsAsync(Season, ct)).OrderBy(t => t.Conference).ThenBy(t => t.City).ToList();
        return true;
    }
}
