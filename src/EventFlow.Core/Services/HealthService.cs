using EventFlow.Core.Interfaces;
using EventFlow.Shared.DTOs;

namespace EventFlow.Core.Services;

public class HealthService : IHealthService
{
    public HealthResponse GetHealth()
    {
        return new HealthResponse();
    }
}