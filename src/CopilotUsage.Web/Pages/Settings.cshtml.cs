using System.ComponentModel.DataAnnotations;
using CopilotUsage.Web.Models;
using CopilotUsage.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CopilotUsage.Web.Pages;

public sealed class SettingsModel(ApiClient api) : PageModel
{
    [BindProperty]
    public UsageDataSourceDto DataSource { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter the GitHub organization login this dashboard tracks.")]
    public string? GitHubOrg { get; set; }

    [BindProperty]
    [Range(0, 100000, ErrorMessage = "Monthly budget must be zero or greater.")]
    public decimal MonthlyBudgetPerSeat { get; set; }

    [BindProperty]
    [Range(0, 480, ErrorMessage = "Minutes saved per request must be between 0 and 480.")]
    public double MinutesSavedPerRequest { get; set; }

    public string? ErrorMessage { get; private set; }

    [TempData]
    public bool Saved { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            var settings = await api.GetSettingsAsync(ct);
            DataSource = settings.DataSource;
            GitHubOrg = settings.GitHubOrg;
            MonthlyBudgetPerSeat = settings.MonthlyBudgetPerSeat;
            MinutesSavedPerRequest = settings.MinutesSavedPerRequest;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await api.SaveSettingsAsync(new UsageSettingsDto
            {
                DataSource = DataSource,
                GitHubOrg = GitHubOrg,
                MonthlyBudgetPerSeat = MonthlyBudgetPerSeat,
                MinutesSavedPerRequest = MinutesSavedPerRequest
            }, ct);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }

        Saved = true;
        return RedirectToPage();
    }
}
