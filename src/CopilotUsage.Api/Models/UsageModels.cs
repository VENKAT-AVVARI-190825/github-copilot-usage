namespace CopilotUsage.Api.Models;

public sealed record SeatInfo(
    string Login,
    string DisplayName,
    DateOnly SeatCreatedAt,
    DateTimeOffset LastActivityAt,
    string LastActivityEditor);

public sealed record UsageRecord(
    DateOnly Date,
    string ActorLogin,
    string Product,
    string Sku,
    decimal Quantity,
    string UnitType,
    decimal PricePerUnit,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount);

public sealed record DailyUsagePoint(DateOnly Date, decimal Requests, decimal NetSpend);

public sealed record MemberUsageSummary(
    string Login,
    string DisplayName,
    decimal RequestsUsed,
    decimal NetSpend,
    double PersonHoursSaved,
    double? CapacityRemainingPct,
    DateTimeOffset LastActivityAt,
    string LastActivityEditor);

public sealed record OrgUsageSummary(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DailyUsagePoint> Daily,
    decimal TotalRequests,
    decimal TotalNetSpend,
    double? AvgCapacityRemainingPct,
    IReadOnlyList<MemberUsageSummary> Members);
