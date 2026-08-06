using Azure.Monitor.OpenTelemetry.Exporter;
using GateKeeper.Core.Interfaces;
using GateKeeper.Core.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using GateKeeper.Storage.Interfaces;
using GateKeeper.Storage.Repositories;
using Microsoft.AspNetCore.Http.Json;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services
        .AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

// Dependency Injection
builder.Services.AddSingleton<IHealthService, HealthService>();
builder.Services.AddSingleton<IEventRepository, EventRepository>();
builder.Services.AddSingleton<ITicketRepository, TicketRepository>();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Build().Run();