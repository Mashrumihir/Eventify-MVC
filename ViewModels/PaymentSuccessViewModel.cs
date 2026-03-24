using Eventify.Models;

namespace Eventify.ViewModels;

public class PaymentSuccessViewModel
{
    public EventItem Event { get; set; } = new();
    public string TicketName { get; set; } = "Regular";
    public int Quantity { get; set; } = 1;
    public decimal TotalPaid { get; set; }
    public string BookingId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "UPI";
}
