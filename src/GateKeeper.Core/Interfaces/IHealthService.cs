using GateKeeper.Shared.DTOs;

namespace GateKeeper.Core.Interfaces;

public interface IHealthService
{
    HealthResponse GetHealth();
}