using CopilotUsage.Api.Models;

namespace CopilotUsage.Api.Services;

public interface IUsageDataProvider
{
    Task<IReadOnlyList<SeatInfo>> GetSeatsAsync(string org, CancellationToken ct = default);

    Task<IReadOnlyList<UsageRecord>> GetUsageRecordsAsync(string org, DateOnly from, DateOnly to, CancellationToken ct = default);
}

public sealed class UsageProviderException(string message, Exception? inner = null) : Exception(message, inner);

public interface IUsageDataProviderFactory
{
    IUsageDataProvider GetProvider(UsageDataSource source);
}
