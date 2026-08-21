using CopilotUsage.Api.Models;

namespace CopilotUsage.Api.Services;

public sealed class UsageAggregationService(IUsageDataProviderFactory providerFactory)
{
    public async Task<OrgUsageSummary> GetOrgSummaryAsync(UsageSettings settings, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var provider = providerFactory.GetProvider(settings.DataSource);

        var seats = await provider.GetSeatsAsync(settings.GitHubOrg, ct);
        var records = await provider.GetUsageRecordsAsync(settings.GitHubOrg, from, to, ct);

        var byActor = records
            .GroupBy(r => r.ActorLogin)
            .ToDictionary(g => g.Key, g => (Requests: g.Sum(r => r.Quantity), NetSpend: g.Sum(r => r.NetAmount)));

        var members = seats
            .Select(seat =>
            {
                var (requests, netSpend) = byActor.TryGetValue(seat.Login, out var totals)
                    ? totals
                    : (0m, 0m);

                double? capacityRemainingPct = settings.MonthlyBudgetPerSeat > 0
                    ? Math.Clamp(1.0 - (double)requests / (double)settings.MonthlyBudgetPerSeat, 0.0, 1.0) * 100.0
                    : null;

                return new MemberUsageSummary(
                    seat.Login,
                    seat.DisplayName,
                    requests,
                    netSpend,
                    capacityRemainingPct,
                    seat.LastActivityAt,
                    seat.LastActivityEditor);
            })
            .OrderByDescending(m => m.RequestsUsed)
            .ToArray();

        var daily = records
            .GroupBy(r => r.Date)
            .Select(g => new DailyUsagePoint(g.Key, g.Sum(r => r.Quantity), g.Sum(r => r.NetAmount)))
            .OrderBy(p => p.Date)
            .ToArray();

        var totalRequests = members.Sum(m => m.RequestsUsed);
        var totalNetSpend = members.Sum(m => m.NetSpend);
        var capacityValues = members.Where(m => m.CapacityRemainingPct.HasValue).Select(m => m.CapacityRemainingPct!.Value).ToArray();
        double? avgCapacityRemaining = capacityValues.Length > 0 ? capacityValues.Average() : null;

        return new OrgUsageSummary(from, to, daily, totalRequests, totalNetSpend, avgCapacityRemaining, members);
    }
}
