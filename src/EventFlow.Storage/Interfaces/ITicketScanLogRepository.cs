using EventFlow.Storage.Entities;

namespace EventFlow.Storage.Interfaces;

public interface ITicketScanLogRepository
{
    Task AddScanAsync(TicketScanLogEntity scan);

    Task<List<TicketScanLogEntity>> GetScansByEventAsync(string eventId);
}
