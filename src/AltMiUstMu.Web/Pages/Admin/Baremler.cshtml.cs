using System.Globalization;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Infrastructure.Services;
using AltMiUstMu.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AltMiUstMu.Web.Pages.Admin;

public class BaremlerModel(SeasonService seasons, GameQueries queries, AdminService admin) : AdminPageModel
{
    /// <summary>Raw text per team id; parsed leniently ("47.5" and "47,5" both work).</summary>
    [BindProperty]
    public Dictionary<int, string> Lines { get; set; } = [];

    public Season? Season { get; private set; }
    public bool Locked { get; private set; }
    public List<TeamOverview> Teams { get; private set; } = [];
    public List<string> Errors { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!await LoadAsync(ct))
        {
            return NotFound();
        }

        Lines = Teams.ToDictionary(t => t.Id, t => t.Line.ToString("0.0", CultureInfo.InvariantCulture));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await LoadAsync(ct))
        {
            return NotFound();
        }

        var parsed = new Dictionary<int, decimal>();
        foreach (var team in Teams)
        {
            var raw = Lines.GetValueOrDefault(team.Id)?.Trim().Replace(',', '.') ?? "";
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                parsed[team.Id] = value;
            }
            else
            {
                Errors.Add($"{team.Abbreviation}: geçerli bir sayı gir.");
            }
        }

        if (Errors.Count == 0)
        {
            Errors = await admin.UpdateLinesAsync(Actor, Season!.Id, parsed, ct);
        }

        if (Errors.Count > 0)
        {
            return Page();
        }

        Flash("Baremler kaydedildi ve puanlar yeniden hesaplandı.");
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
        Teams = (await queries.GetTeamsAsync(Season, ct)).OrderBy(t => t.Conference).ThenBy(t => t.City).ToList();
        return true;
    }
}
