using System.Text.Json.Serialization;

namespace CopilotUsage.Web.Models;

// Mirrors CopilotUsage.Api's public JSON contract. Kept as plain DTOs rather
// than a shared project reference, since the API and Web app are two
// independently deployable services that only talk over HTTP.

public sealed record DailyUsagePointDto(DateOnly Date, decimal Requests, decimal NetSpend);

public sealed record MemberUsageSummaryDto(
    string Login,
    string DisplayName,
    decimal RequestsUsed,
    decimal NetSpend,
    double PersonHoursSaved,
    double? CapacityRemainingPct,
    DateTimeOffset LastActivityAt,
    string LastActivityEditor);

public sealed record OrgUsageSummaryDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DailyUsagePointDto> Daily,
    decimal TotalRequests,
    decimal TotalNetSpend,
    double? AvgCapacityRemainingPct,
    IReadOnlyList<MemberUsageSummaryDto> Members);

[JsonConverter(typeof(JsonStringEnumConverter<UsageDataSourceDto>))]
public enum UsageDataSourceDto
{
    Mock,
    GitHubLive
}

public sealed class UsageSettingsDto
{
    public UsageDataSourceDto DataSource { get; set; } = UsageDataSourceDto.Mock;
    public string? GitHubOrg { get; set; }
    public decimal MonthlyBudgetPerSeat { get; set; } = 300m;
    public double MinutesSavedPerRequest { get; set; } = 5.0;
}
