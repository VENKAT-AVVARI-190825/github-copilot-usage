using System.Text.Json;
using CopilotUsage.Web.Models;
using CopilotUsage.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CopilotUsage.Web.Pages;

public sealed class DashboardModel(ApiClient api) : PageModel
{
    public OrgUsageSummaryDto? Summary { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string RequestsChartJson { get; private set; } = "null";

    public string SpendChartJson { get; private set; } = "null";

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Summary = await api.GetSummaryAsync(ct: ct);

            RequestsChartJson = JsonSerializer.Serialize(new
            {
                labels = Summary.Daily.Select(d => d.Date.ToString("MMM d")),
                values = Summary.Daily.Select(d => d.Requests)
            });

            SpendChartJson = JsonSerializer.Serialize(new
            {
                labels = Summary.Daily.Select(d => d.Date.ToString("MMM d")),
                values = Summary.Daily.Select(d => d.NetSpend)
            });
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
