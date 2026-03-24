namespace Eventify.Models;

public class OrganizerAnnouncement
{
    public int Id { get; set; }
    public int? EventItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public EventItem? EventItem { get; set; }
}
