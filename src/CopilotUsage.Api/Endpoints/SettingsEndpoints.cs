using CopilotUsage.Api.Models;
using CopilotUsage.Api.Services;

namespace CopilotUsage.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/", async (ISettingsStore store, CancellationToken ct) =>
            Results.Ok(await store.GetAsync(ct)))
            .WithName("GetSettings");

        group.MapPut("/", async (UsageSettings settings, ISettingsStore store, CancellationToken ct) =>
        {
            if (settings.MonthlyBudgetPerSeat < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["MonthlyBudgetPerSeat"] = ["Value must be zero or greater."]
                });
            }

            var saved = await store.SaveAsync(settings, ct);
            return Results.Ok(saved);
        })
        .WithName("SaveSettings");

        return app;
    }
}
