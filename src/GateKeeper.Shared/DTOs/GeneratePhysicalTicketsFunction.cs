namespace GateKeeper.Shared.DTOs;

public class GeneratePhysicalTicketsRequest
{
    // =====================================
    // EVENT
    // =====================================

    public string EventId { get; set; } = "";


    // =====================================
    // TICKET TYPE
    // =====================================

    public string TicketType { get; set; } = "";


    // =====================================
    // QUANTITY
    // =====================================

    public int Quantity { get; set; }
}