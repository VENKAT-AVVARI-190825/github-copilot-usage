using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CopilotUsage.Api.Models;
using Microsoft.Extensions.Options;

namespace CopilotUsage.Api.Services;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>
    /// Personal access token with organization billing read access.
    /// Sourced from configuration/environment (e.g. GITHUB__TOKEN), never
    /// from the user-editable settings file, since it's a secret.
    /// </summary>
    public string? Token { get; set; }
}

/// <summary>
/// Calls GitHub's real Billing Usage and Copilot seats APIs. Requires org
/// owner/billing-manager access on the configured token; unreachable until
/// that access exists, but implemented now since it's the stated end goal.
/// </summary>
public sealed class GitHubBillingUsageProvider : IUsageDataProvider
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<GitHubOptions> _options;
    private AuthenticatedUser? _cachedUser;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GitHubBillingUsageProvider(HttpClient http, IOptionsMonitor<GitHubOptions> options)
    {
        _http = http;
        _options = options;
    }

    public async Task<IReadOnlyList<SeatInfo>> GetSeatsAsync(string? org, CancellationToken ct = default)
    {
        EnsureAuthorized();

        if (string.IsNullOrWhiteSpace(org))
        {
            // No org access — fall back to the authenticated user's own
            // personal Copilot subscription as the sole "seat".
            var me = await GetAuthenticatedUserAsync(ct);
            return
            [
                new SeatInfo(
                    me.Login,
                    me.Name ?? me.Login,
                    DateOnly.FromDateTime(me.CreatedAt.UtcDateTime),
                    me.CreatedAt,
                    "personal account")
            ];
        }

        var seats = new List<SeatInfo>();
        var page = 1;

        while (true)
        {
            using var request = CreateRequest(HttpMethod.Get, $"orgs/{org}/copilot/billing/seats?per_page=100&page={page}");
            using var response = await _http.SendAsync(request, ct);
            await EnsureSuccessAsync(response, ct);

            var payload = await response.Content.ReadFromJsonAsync<SeatsResponse>(JsonOptions, ct);
            if (payload?.Seats is null || payload.Seats.Count == 0)
            {
                break;
            }

            seats.AddRange(payload.Seats
                .Where(s => s.Assignee is not null)
                .Select(s => new SeatInfo(
                    s.Assignee!.Login,
                    s.Assignee.Login,
                    DateOnly.FromDateTime(s.CreatedAt.UtcDateTime),
                    s.LastActivityAt ?? s.CreatedAt,
                    s.LastActivityEditor ?? "unknown")));

            if (payload.Seats.Count < 100)
            {
                break;
            }

            page++;
        }

        return seats;
    }

    public async Task<IReadOnlyList<UsageRecord>> GetUsageRecordsAsync(string? org, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureAuthorized();

        string usageUrl;
        if (string.IsNullOrWhiteSpace(org))
        {
            var me = await GetAuthenticatedUserAsync(ct);
            usageUrl = $"users/{me.Login}/settings/billing/usage";
        }
        else
        {
            usageUrl = $"organizations/{org}/settings/billing/usage";
        }

        using var request = CreateRequest(HttpMethod.Get, usageUrl);
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        var payload = await response.Content.ReadFromJsonAsync<BillingUsageResponse>(JsonOptions, ct);
        if (payload?.UsageItems is null)
        {
            return [];
        }

        return payload.UsageItems
            .Where(i => string.Equals(i.Product, "Copilot", StringComparison.OrdinalIgnoreCase))
            .Select(i => new
            {
                Item = i,
                Date = DateOnly.FromDateTime(i.UsageAt.UtcDateTime)
            })
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => new UsageRecord(
                x.Date,
                x.Item.ActorName ?? "unknown",
                x.Item.Product,
                x.Item.Sku,
                x.Item.Quantity,
                x.Item.UnitType,
                x.Item.PricePerUnit,
                x.Item.GrossAmount,
                x.Item.DiscountAmount,
                x.Item.NetAmount))
            .ToArray();
    }

    private async Task<AuthenticatedUser> GetAuthenticatedUserAsync(CancellationToken ct)
    {
        if (_cachedUser is { } cached)
        {
            return cached;
        }

        using var request = CreateRequest(HttpMethod.Get, "user");
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        var user = await response.Content.ReadFromJsonAsync<AuthenticatedUser>(JsonOptions, ct)
            ?? throw new UsageProviderException("GitHub API returned an empty response for the authenticated user.");

        _cachedUser = user;
        return user;
    }

    private void EnsureAuthorized()
    {
        if (string.IsNullOrWhiteSpace(_options.CurrentValue.Token))
        {
            throw new UsageProviderException(
                "GitHub live data source is selected but no token is configured. Set the GITHUB__TOKEN environment variable to a PAT with org billing read access.");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.CurrentValue.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new UsageProviderException(
            $"GitHub API request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }

    private sealed record SeatsResponse(
        [property: JsonPropertyName("total_seats")] int TotalSeats,
        [property: JsonPropertyName("seats")] List<Seat> Seats);

    private sealed record Seat(
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("last_activity_at")] DateTimeOffset? LastActivityAt,
        [property: JsonPropertyName("last_activity_editor")] string? LastActivityEditor,
        [property: JsonPropertyName("assignee")] Assignee? Assignee);

    private sealed record Assignee([property: JsonPropertyName("login")] string Login);

    private sealed record AuthenticatedUser(
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

    private sealed record BillingUsageResponse(
        [property: JsonPropertyName("usageItems")] List<UsageItem> UsageItems);

    private sealed record UsageItem(
        [property: JsonPropertyName("date")] DateTimeOffset UsageAt,
        [property: JsonPropertyName("product")] string Product,
        [property: JsonPropertyName("sku")] string Sku,
        [property: JsonPropertyName("quantity")] decimal Quantity,
        [property: JsonPropertyName("unitType")] string UnitType,
        [property: JsonPropertyName("pricePerUnit")] decimal PricePerUnit,
        [property: JsonPropertyName("grossAmount")] decimal GrossAmount,
        [property: JsonPropertyName("discountAmount")] decimal DiscountAmount,
        [property: JsonPropertyName("netAmount")] decimal NetAmount,
        [property: JsonPropertyName("actorName")] string? ActorName);
}
