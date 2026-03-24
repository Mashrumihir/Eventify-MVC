namespace Eventify.ViewModels;

public class MyBookingsViewModel
{
    public string ActiveTab { get; set; } = "all";
    public List<MyBookingItemViewModel> Items { get; set; } = new();
}

public class MyBookingItemViewModel
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string TimeRange { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string TicketLabel { get; set; } = "Regular";
    public string AccessLabel { get; set; } = "Regular Access";
    public decimal Price { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsCanceled { get; set; }
}
