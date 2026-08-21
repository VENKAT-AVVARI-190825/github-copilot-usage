using CopilotUsage.Api.Models;

namespace CopilotUsage.Api.Services;

public sealed class UsageDataProviderFactory(IServiceProvider services) : IUsageDataProviderFactory
{
    public IUsageDataProvider GetProvider(UsageDataSource source) => source switch
    {
        UsageDataSource.Mock => services.GetRequiredService<MockUsageDataProvider>(),
        UsageDataSource.GitHubLive => services.GetRequiredService<GitHubBillingUsageProvider>(),
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown data source")
    };
}
