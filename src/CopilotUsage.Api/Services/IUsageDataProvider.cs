using CopilotUsage.Api.Models;

namespace CopilotUsage.Api.Services;

public interface IUsageDataProvider
{
    /// <summary>
    /// <paramref name="org"/> is null/empty when no org is configured — the
    /// GitHub-live provider then falls back to the authenticated user's own
    /// personal Copilot subscription instead of an org's seats.
    /// </summary>
    Task<IReadOnlyList<SeatInfo>> GetSeatsAsync(string? org, CancellationToken ct = default);

    Task<IReadOnlyList<UsageRecord>> GetUsageRecordsAsync(string? org, DateOnly from, DateOnly to, CancellationToken ct = default);
}

public sealed class UsageProviderException(string message, Exception? inner = null) : Exception(message, inner);

public interface IUsageDataProviderFactory
{
    IUsageDataProvider GetProvider(UsageDataSource source);
}
