using System.Text.Json.Serialization;
using CopilotUsage.Api.Endpoints;
using CopilotUsage.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISettingsStore, JsonFileSettingsStore>();
builder.Services.AddSingleton<MockUsageDataProvider>();
builder.Services.AddSingleton<IUsageDataProviderFactory, UsageDataProviderFactory>();
builder.Services.AddScoped<UsageAggregationService>();

builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));
builder.Services.AddHttpClient<GitHubBillingUsageProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CopilotUsageDashboard/1.0");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapUsageEndpoints();
app.MapSettingsEndpoints();

app.Run();
