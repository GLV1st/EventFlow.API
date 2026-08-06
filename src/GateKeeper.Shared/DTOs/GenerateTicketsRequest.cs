namespace GateKeeper.Shared.DTOs;

public class GenerateTicketsRequest
{
    public string EventId { get; set; } = "";

    public string OrderId { get; set; } = "";

    public int Quantity { get; set; }

    public string TicketType { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public string CustomerEmail { get; set; } = "";
}