using CopilotUsage.Api.Models;

namespace CopilotUsage.Api.Services;

public interface ISettingsStore
{
    Task<UsageSettings> GetAsync(CancellationToken ct = default);

    Task<UsageSettings> SaveAsync(UsageSettings settings, CancellationToken ct = default);
}
