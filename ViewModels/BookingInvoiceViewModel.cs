namespace Eventify.ViewModels;

public class BookingInvoiceViewModel
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime IssuedOn { get; set; }

    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string EventTime { get; set; } = string.Empty;
    public string EventLocation { get; set; } = string.Empty;

    public string UserName { get; set; } = "attend";
    public string UserEmail { get; set; } = "attend@eventify.com";
    public string UserPhone { get; set; } = "+91 90000 00000";

    public string TicketName { get; set; } = "Regular";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}
