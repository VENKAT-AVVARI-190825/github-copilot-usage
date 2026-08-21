using CopilotUsage.Api.Services;

namespace CopilotUsage.Api.Endpoints;

public static class UsageEndpoints
{
    public static IEndpointRouteBuilder MapUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/usage").WithTags("Usage");

        group.MapGet("/summary", async (
            DateOnly? from,
            DateOnly? to,
            ISettingsStore settingsStore,
            UsageAggregationService aggregation,
            CancellationToken ct) =>
        {
            var settings = await settingsStore.GetAsync(ct);
            var effectiveTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var effectiveFrom = from ?? effectiveTo.AddDays(-29);

            try
            {
                var summary = await aggregation.GetOrgSummaryAsync(settings, effectiveFrom, effectiveTo, ct);
                return Results.Ok(summary);
            }
            catch (UsageProviderException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithName("GetUsageSummary");

        return app;
    }
}
