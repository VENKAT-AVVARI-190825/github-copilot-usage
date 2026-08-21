using CopilotUsage.Api.Services;
using Microsoft.Extensions.Hosting;

namespace CopilotUsage.Api.Tests;

public class MockUsageDataProviderTests
{
    private static MockUsageDataProvider CreateProvider(DateTimeOffset now) =>
        new(new FakeHostEnvironment(), new FakeTimeProvider(now));

    [Fact]
    public async Task GetUsageRecordsAsync_IsDeterministicAcrossCalls()
    {
        var provider = CreateProvider(new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero));
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 31);

        var first = await provider.GetUsageRecordsAsync("acme", from, to);
        var second = await provider.GetUsageRecordsAsync("acme", from, to);

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(
            first.Select(r => (r.Date, r.ActorLogin, r.Quantity)),
            second.Select(r => (r.Date, r.ActorLogin, r.Quantity)));
    }

    [Fact]
    public async Task GetUsageRecordsAsync_NeverGeneratesUsageBeforeSeatCreation()
    {
        var provider = CreateProvider(new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero));
        // hmuller's seat (roster.json) starts 2025-05-08 — request a window entirely before that.
        var records = await provider.GetUsageRecordsAsync("acme", new DateOnly(2025, 1, 1), new DateOnly(2025, 5, 7));

        Assert.DoesNotContain(records, r => r.ActorLogin == "hmuller");
    }

    [Fact]
    public async Task GetSeatsAsync_ReturnsAllRosterMembers()
    {
        var provider = CreateProvider(new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero));
        var seats = await provider.GetSeatsAsync("acme");

        Assert.Equal(8, seats.Count);
        Assert.Contains(seats, s => s.Login == "gzhang");
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "CopilotUsage.Api";
        public string ContentRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory);
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
