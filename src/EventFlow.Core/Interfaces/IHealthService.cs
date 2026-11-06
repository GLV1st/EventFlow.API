using EventFlow.Shared.DTOs;

namespace EventFlow.Core.Interfaces;

public interface IHealthService
{
    HealthResponse GetHealth();
}