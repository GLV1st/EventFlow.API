namespace GateKeeper.Shared.DTOs;

public class CheckInResponse
{
    public string Status { get; set; } = "";

    public string Message { get; set; } = "";

    public string TicketNumber { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public string TicketType { get; set; } = "";
}