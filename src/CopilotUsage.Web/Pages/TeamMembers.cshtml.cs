using System.Text.Json;
using CopilotUsage.Web.Models;
using CopilotUsage.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CopilotUsage.Web.Pages;

public sealed class TeamMembersModel(ApiClient api) : PageModel
{
    public IReadOnlyList<MemberUsageSummaryDto> Members { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public string MembersChartJson { get; private set; } = "null";

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            var summary = await api.GetSummaryAsync(ct: ct);
            Members = summary.Members;

            MembersChartJson = JsonSerializer.Serialize(new
            {
                labels = Members.Select(m => m.DisplayName),
                values = Members.Select(m => m.RequestsUsed)
            });
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public static string CapacityStatus(double? pct) => pct switch
    {
        null => "unknown",
        >= 50 => "good",
        >= 20 => "warning",
        _ => "critical"
    };

    public static string CapacityIcon(double? pct) => CapacityStatus(pct) switch
    {
        "good" => "●",
        "warning" => "▲",
        "critical" => "■",
        _ => "?"
    };
}
