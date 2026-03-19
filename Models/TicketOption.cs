namespace Eventify.Models;

public class TicketOption
{
    public int Id { get; set; }
    public int EventItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Features { get; set; } = string.Empty;

    public EventItem? EventItem { get; set; }
}
