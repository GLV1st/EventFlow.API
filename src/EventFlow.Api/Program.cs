
using Azure.Monitor.OpenTelemetry.Exporter;
using EventFlow.Api.Security;
using EventFlow.Core.Interfaces;
using EventFlow.Core.Services;
using EventFlow.Storage.Interfaces;
using EventFlow.Storage.Repositories;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable(
        "APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services
        .AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

// =====================================
// CORE SERVICES
// =====================================

builder.Services.AddSingleton<
    IHealthService,
    HealthService>();

// =====================================
// STORAGE REPOSITORIES
// =====================================

builder.Services.AddSingleton<
    IEventRepository,
    EventRepository>();

builder.Services.AddSingleton<
    ITicketRepository,
    TicketRepository>();

builder.Services.AddSingleton<
    ITicketScanLogRepository,
    TicketScanLogRepository>();

builder.Services.AddSingleton<
    ITenantRegistryRepository,
    TenantRegistryRepository>();

// =====================================
// TENANT SECURITY
// =====================================

builder.Services.AddSingleton<
    ITenantSecurityService,
    TenantSecurityService>();

// =====================================
// JSON
// =====================================

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Build().Run();
