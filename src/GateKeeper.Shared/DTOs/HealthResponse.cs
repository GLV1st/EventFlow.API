namespace GateKeeper.Shared.DTOs;

public class HealthResponse
{
    public string Application { get; set; } = "GateKeeper";

    public string Version { get; set; } = "0.1.0";

    public string Status { get; set; } = "Healthy";

    public string EnvironmentName { get; set; } =
        System.Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Development";

    public DateTime UtcTime { get; set; } = DateTime.UtcNow;
}