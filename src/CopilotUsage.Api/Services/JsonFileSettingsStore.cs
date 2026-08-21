using System.Text.Json;
using CopilotUsage.Api.Models;

namespace CopilotUsage.Api.Services;

public sealed class JsonFileSettingsStore : ISettingsStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public JsonFileSettingsStore(IHostEnvironment env)
    {
        var appDataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataDir);
        _filePath = Path.Combine(appDataDir, "settings.json");
    }

    public async Task<UsageSettings> GetAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath))
            {
                return new UsageSettings();
            }

            using var stream = File.OpenRead(_filePath);
            var settings = await JsonSerializer.DeserializeAsync<UsageSettings>(stream, cancellationToken: ct);
            return settings ?? new UsageSettings();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<UsageSettings> SaveAsync(UsageSettings settings, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, ct);
            return settings;
        }
        finally
        {
            _lock.Release();
        }
    }
}
