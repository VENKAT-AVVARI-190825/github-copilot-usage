using CopilotUsage.Api.Models;
using CopilotUsage.Api.Services;

namespace CopilotUsage.Api.Tests;

public class UsageAggregationServiceTests
{
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly To = new(2026, 8, 3);

    private static UsageAggregationService CreateService(
        IReadOnlyList<SeatInfo> seats,
        IReadOnlyList<UsageRecord> records) =>
        new(new StubProviderFactory(new StubProvider(seats, records)));

    [Fact]
    public async Task GetOrgSummaryAsync_ComputesPersonHoursFromRequestsAndMinutesSetting()
    {
        var seats = new[] { Seat("dev1") };
        var records = new[] { Record("dev1", From, quantity: 120m) };
        var service = CreateService(seats, records);

        var summary = await service.GetOrgSummaryAsync(
            new UsageSettings { GitHubOrg = "acme", MinutesSavedPerRequest = 5, MonthlyBudgetPerSeat = 300 },
            From, To);

        var member = Assert.Single(summary.Members);
        Assert.Equal(120m, member.RequestsUsed);
        Assert.Equal(10.0, member.PersonHoursSaved, precision: 3); // 120 * 5 / 60
    }

    [Theory]
    [InlineData(0, 300, 100.0)]     // no usage -> full capacity remaining
    [InlineData(150, 300, 50.0)]    // half budget used
    [InlineData(450, 300, 0.0)]     // over budget clamps to 0, never negative
    public async Task GetOrgSummaryAsync_ComputesCapacityRemainingPctAndClamps(decimal requests, decimal budget, double expectedPct)
    {
        var seats = new[] { Seat("dev1") };
        var records = requests > 0 ? new[] { Record("dev1", From, requests) } : [];
        var service = CreateService(seats, records);

        var summary = await service.GetOrgSummaryAsync(
            new UsageSettings { GitHubOrg = "acme", MonthlyBudgetPerSeat = budget },
            From, To);

        Assert.Equal(expectedPct, Assert.Single(summary.Members).CapacityRemainingPct);
    }

    [Fact]
    public async Task GetOrgSummaryAsync_ZeroBudget_ReportsNoCapacityInsteadOfDividingByZero()
    {
        var seats = new[] { Seat("dev1") };
        var service = CreateService(seats, []);

        var summary = await service.GetOrgSummaryAsync(
            new UsageSettings { GitHubOrg = "acme", MonthlyBudgetPerSeat = 0 },
            From, To);

        Assert.Null(Assert.Single(summary.Members).CapacityRemainingPct);
        Assert.Null(summary.AvgCapacityRemainingPct);
    }

    [Fact]
    public async Task GetOrgSummaryAsync_IncludesSeatsWithNoUsageRecords()
    {
        var seats = new[] { Seat("dev1"), Seat("dev2") };
        var records = new[] { Record("dev1", From, 10m) };
        var service = CreateService(seats, records);

        var summary = await service.GetOrgSummaryAsync(
            new UsageSettings { GitHubOrg = "acme" }, From, To);

        Assert.Equal(2, summary.Members.Count);
        var dev2 = summary.Members.Single(m => m.Login == "dev2");
        Assert.Equal(0m, dev2.RequestsUsed);
        Assert.Equal(0.0, dev2.PersonHoursSaved);
    }

    [Fact]
    public async Task GetOrgSummaryAsync_AggregatesDailyTotalsAcrossMembers()
    {
        var seats = new[] { Seat("dev1"), Seat("dev2") };
        var records = new[]
        {
            Record("dev1", From, 10m),
            Record("dev2", From, 5m),
            Record("dev1", From.AddDays(1), 8m)
        };
        var service = CreateService(seats, records);

        var summary = await service.GetOrgSummaryAsync(new UsageSettings { GitHubOrg = "acme" }, From, To);

        var firstDay = summary.Daily.Single(d => d.Date == From);
        Assert.Equal(15m, firstDay.Requests);
        Assert.Equal(23m, summary.TotalRequests);
    }

    [Fact]
    public async Task GetOrgSummaryAsync_MissingOrg_ThrowsUsageProviderException()
    {
        var service = CreateService([], []);

        await Assert.ThrowsAsync<UsageProviderException>(() =>
            service.GetOrgSummaryAsync(new UsageSettings { GitHubOrg = null }, From, To));
    }

    private static SeatInfo Seat(string login) =>
        new(login, login, From, new DateTimeOffset(From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), "vscode");

    private static UsageRecord Record(string login, DateOnly date, decimal quantity) =>
        new(date, login, "Copilot", "copilot_premium_request", quantity, "Request", 0.04m, quantity * 0.04m, 0m, quantity * 0.04m);

    private sealed class StubProvider(IReadOnlyList<SeatInfo> seats, IReadOnlyList<UsageRecord> records) : IUsageDataProvider
    {
        public Task<IReadOnlyList<SeatInfo>> GetSeatsAsync(string org, CancellationToken ct = default) =>
            Task.FromResult(seats);

        public Task<IReadOnlyList<UsageRecord>> GetUsageRecordsAsync(string org, DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult(records);
    }

    private sealed class StubProviderFactory(IUsageDataProvider provider) : IUsageDataProviderFactory
    {
        public IUsageDataProvider GetProvider(UsageDataSource source) => provider;
    }
}
