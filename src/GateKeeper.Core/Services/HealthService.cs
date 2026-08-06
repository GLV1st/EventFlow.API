using GateKeeper.Core.Interfaces;
using GateKeeper.Shared.DTOs;

namespace GateKeeper.Core.Services;

public class HealthService : IHealthService
{
    public HealthResponse GetHealth()
    {
        return new HealthResponse();
    }
}