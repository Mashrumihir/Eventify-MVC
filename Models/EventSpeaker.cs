namespace Eventify.Models;

public class EventSpeaker
{
    public int Id { get; set; }
    public int EventItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    public EventItem? EventItem { get; set; }
}
