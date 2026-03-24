namespace Eventify.Models;

public class Booking
{
    public int Id { get; set; }
    public int EventItemId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Status { get; set; } = "Booked";
    public bool IsSaved { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public EventItem? EventItem { get; set; }
}
