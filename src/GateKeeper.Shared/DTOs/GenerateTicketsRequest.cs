namespace GateKeeper.Shared.DTOs;

public class GenerateTicketsRequest
{
    // =====================================
    // EVENT
    // =====================================

    public string EventId { get; set; } = "";

    public string EventName { get; set; } = "";

    public string EventDate { get; set; } = "";


    // =====================================
    // TICKET NUMBERING
    // =====================================

    public string TicketPrefix { get; set; } = "";


    // =====================================
    // ORDER
    // =====================================

    public string OrderId { get; set; } = "";

    public int Quantity { get; set; }


    // =====================================
    // TICKET
    // =====================================

    public string TicketType { get; set; } = "";


    // =====================================
    // CUSTOMER
    // =====================================

    public string CustomerName { get; set; } = "";

    public string CustomerEmail { get; set; } = "";
}