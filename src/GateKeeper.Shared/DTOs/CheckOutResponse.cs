namespace GateKeeper.Shared.DTOs;

public class CheckOutResponse
{
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";

    public string TicketNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string TicketType { get; set; } = "";
    public int PeopleOnSite { get; set; } = 0;
    public int PeopleBooked { get; set; } = 0;
}