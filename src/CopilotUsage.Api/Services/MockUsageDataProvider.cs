using System.Text.Json;
using System.Text.Json.Serialization;
using CopilotUsage.Api.Models;

namespace CopilotUsage.Api.Services;

public sealed class MockUsageDataProvider : IUsageDataProvider
{
    private const decimal PricePerRequest = 0.04m;
    private const string CopilotProduct = "Copilot";
    private const string PremiumRequestSku = "copilot_premium_request";

    private readonly string _rosterPath;
    private readonly TimeProvider _timeProvider;
    private IReadOnlyList<RosterMember>? _roster;

    public MockUsageDataProvider(IHostEnvironment env, TimeProvider timeProvider)
    {
        _rosterPath = Path.Combine(env.ContentRootPath, "Data", "roster.json");
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<SeatInfo>> GetSeatsAsync(string org, CancellationToken ct = default)
    {
        var roster = await LoadRosterAsync(ct);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        return roster
            .Select(m => new SeatInfo(
                m.Login,
                m.DisplayName,
                m.SeatCreatedAt,
                LastActivityFor(m, today),
                m.LastActivityEditor))
            .ToArray();
    }

    public async Task<IReadOnlyList<UsageRecord>> GetUsageRecordsAsync(string org, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var roster = await LoadRosterAsync(ct);
        var records = new List<UsageRecord>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            foreach (var member in roster)
            {
                var quantity = DailyQuantityFor(member, date);
                if (quantity <= 0)
                {
                    continue;
                }

                var gross = Math.Round(quantity * PricePerRequest, 2);
                records.Add(new UsageRecord(
                    date,
                    member.Login,
                    CopilotProduct,
                    PremiumRequestSku,
                    quantity,
                    "Request",
                    PricePerRequest,
                    gross,
                    0m,
                    gross));
            }
        }

        return records;
    }

    private static DateTimeOffset LastActivityFor(RosterMember member, DateOnly today)
    {
        // Walk back from today to find the most recent day the deterministic
        // generator assigns nonzero usage, so "last active" stays consistent
        // with the generated usage records instead of drifting independently.
        var earliest = today.AddDays(-14);
        for (var date = today; date >= member.SeatCreatedAt && date >= earliest; date = date.AddDays(-1))
        {
            if (DailyQuantityFor(member, date) > 0)
            {
                return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue).AddHours(15), TimeSpan.Zero);
            }
        }

        return new DateTimeOffset(member.SeatCreatedAt.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    private static decimal DailyQuantityFor(RosterMember member, DateOnly date)
    {
        if (date < member.SeatCreatedAt)
        {
            return 0m;
        }

        // Deterministic pseudo-random noise seeded from login+date, so the
        // same date always yields the same value across requests without
        // persisting a generated series anywhere.
        var seed = HashCode.Combine(member.Login, date.DayNumber);
        var rng = new Random(seed);

        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var baseline = member.AvgDailyRequests * (isWeekend ? 0.25 : 1.0);
        var noiseFactor = 0.6 + rng.NextDouble() * 0.8; // 60%-140% of baseline
        var value = Math.Round(baseline * noiseFactor, 1);

        return (decimal)Math.Max(0, value);
    }

    private async Task<IReadOnlyList<RosterMember>> LoadRosterAsync(CancellationToken ct)
    {
        if (_roster is not null)
        {
            return _roster;
        }

        using var stream = File.OpenRead(_rosterPath);
        var roster = await JsonSerializer.DeserializeAsync<List<RosterMember>>(stream, JsonOptions, ct);
        _roster = roster ?? [];
        return _roster;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record RosterMember(
        string Login,
        string DisplayName,
        DateOnly SeatCreatedAt,
        string LastActivityEditor,
        double AvgDailyRequests);
}
