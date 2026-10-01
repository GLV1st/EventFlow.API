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
// WALLET SERVICES
// =====================================

var walletSettings =
    GateKeeper.Api.Wallet.WalletSettings.FromEnvironment();

builder.Services.AddSingleton(walletSettings);

builder.Services.AddHttpClient("wallet");

builder.Services.AddSingleton<
    GateKeeper.Api.Wallet.AppleWalletService>();

builder.Services.AddSingleton<
    GateKeeper.Api.Wallet.GoogleWalletService>();


// =====================================
// DEPENDENCY INJECTION
// =====================================

builder.Services.AddSingleton<
    IHealthService,
    HealthService>();

builder.Services.AddSingleton<
    IEventRepository,
    EventRepository>();

builder.Services.AddSingleton<
    ITicketRepository,
    TicketRepository>();


// =====================================
// TRADER SYSTEM
// =====================================

builder.Services.AddSingleton<
    ITraderRepository,
    TraderRepository>();
builder.Services.AddSingleton<
    ITraderPitchRepository,
    TraderPitchRepository>();

// =====================================
// CATERING VENDOR SYSTEM
// =====================================

builder.Services.AddSingleton<
    ICateringVendorRepository,
    CateringVendorRepository>();


// =====================================
// JSON
// =====================================

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});


builder.Build().Run();