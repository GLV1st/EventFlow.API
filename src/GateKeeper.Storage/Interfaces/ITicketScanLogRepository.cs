using GateKeeper.Storage.Entities;

namespace GateKeeper.Storage.Interfaces;

public interface ITicketScanLogRepository
{
    Task AddScanAsync(TicketScanLogEntity scan);

    Task<List<TicketScanLogEntity>> GetScansByEventAsync(string eventId);
}
