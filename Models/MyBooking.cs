namespace Eventify.Models;

public class MyBooking
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int EventItemId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string TicketName { get; set; } = "Regular";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Booked";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public EventItem? EventItem { get; set; }
}
