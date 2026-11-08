
using EventFlow.Storage.Entities;

namespace EventFlow.Storage.Interfaces;

public interface ITicketScanLogRepository
{
    Task AddScanAsync(
        string tenantId,
        TicketScanLogEntity scan);

    Task<List<TicketScanLogEntity>> GetScansByEventAsync(
        string tenantId,
        string eventId);
}
