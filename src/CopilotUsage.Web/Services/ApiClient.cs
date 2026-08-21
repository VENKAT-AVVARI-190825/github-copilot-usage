using System.Net.Http.Json;
using System.Text.Json;
using CopilotUsage.Web.Models;

namespace CopilotUsage.Web.Services;

public sealed class ApiException(string message) : Exception(message);

public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<OrgUsageSummaryDto> GetSummaryAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (from is { } f) query.Add($"from={f:yyyy-MM-dd}");
        if (to is { } t) query.Add($"to={t:yyyy-MM-dd}");
        var url = "api/usage/summary" + (query.Count > 0 ? "?" + string.Join('&', query) : "");

        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await ReadErrorAsync(response, ct));
        }

        return await response.Content.ReadFromJsonAsync<OrgUsageSummaryDto>(JsonOptions, ct)
            ?? throw new ApiException("The usage service returned an empty response.");
    }

    public async Task<UsageSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/settings", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await ReadErrorAsync(response, ct));
        }

        return await response.Content.ReadFromJsonAsync<UsageSettingsDto>(JsonOptions, ct)
            ?? new UsageSettingsDto();
    }

    public async Task<UsageSettingsDto> SaveSettingsAsync(UsageSettingsDto settings, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync("api/settings", settings, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await ReadErrorAsync(response, ct));
        }

        return await response.Content.ReadFromJsonAsync<UsageSettingsDto>(JsonOptions, ct)
            ?? settings;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Request failed with status {(int)response.StatusCode}.";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body; fall through to returning it as-is.
        }

        return body;
    }
}
