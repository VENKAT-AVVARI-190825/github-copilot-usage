using System.Text.Json.Serialization;

namespace CopilotUsage.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter<UsageDataSource>))]
public enum UsageDataSource
{
    Mock,
    GitHubLive
}

public sealed class UsageSettings
{
    public UsageDataSource DataSource { get; set; } = UsageDataSource.Mock;

    public string? GitHubOrg { get; set; }

    public decimal MonthlyBudgetPerSeat { get; set; } = 300m;
}
