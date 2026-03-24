namespace Eventify.Models;

public class OrganizerEventConfig
{
    public int Id { get; set; }
    public int EventItemId { get; set; }
    public int AvailableQuantity { get; set; }
    public bool EarlyBirdDiscount { get; set; }
    public decimal EarlyBirdPrice { get; set; }
    public string RefundPolicy { get; set; } = string.Empty;
    public string GalleryImagesJson { get; set; } = string.Empty;
}

