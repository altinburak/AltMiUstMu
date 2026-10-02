using System.ComponentModel.DataAnnotations;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Time;
using AltMiUstMu.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace AltMiUstMu.Web.Pages.Admin;

public class SezonModel(SeasonService seasons, AdminService admin) : AdminPageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Season? Season { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Sezon adı zorunlu.")]
        [Display(Name = "Sezon adı")]
        public string Label { get; set; } = "";

        [Required(ErrorMessage = "Kilit zamanı zorunlu.")]
        [Display(Name = "Kilit zamanı (TSİ)")]
        public DateTime LockAtLocal { get; set; }

        [Range(1, 100, ErrorMessage = "1 ile 100 arasında olmalı.")]
        [Display(Name = "Takım başına maç")]
        public int GamesPerTeam { get; set; } = 82;

        [Display(Name = "Durum")]
        public SeasonStatus Status { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Label = Season.Label,
            LockAtLocal = Istanbul.FromUtc(Season.LockAt),
            GamesPerTeam = Season.GamesPerTeam,
            Status = Season.Status,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Season = await seasons.GetCurrentAsync(ct);
        if (Season is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var error = await admin.UpdateSeasonAsync(Actor, Season.Id, Input.Label, Istanbul.ToUtc(Input.LockAtLocal), Input.GamesPerTeam, Input.Status, ct);
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            return Page();
        }

        Flash("Sezon ayarları kaydedildi.");
        return RedirectToPage();
    }
}
