using Eventify.Models;

namespace Eventify.ViewModels;

public class PaymentViewModel
{
    public EventItem Event { get; set; } = new();
    public string TicketName { get; set; } = "Regular";
    public decimal TicketPrice { get; set; } = 99;
    public int Quantity { get; set; } = 1;
    public decimal Discount { get; set; }
    public decimal Total => (TicketPrice * Math.Max(1, Quantity)) - Discount;
}
