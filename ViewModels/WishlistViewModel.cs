namespace Eventify.ViewModels;

public class WishlistViewModel
{
    public List<WishlistItemViewModel> Items { get; set; } = new();
}

public class WishlistItemViewModel
{
    public int EventId { get; set; }
    public int BookingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
}
