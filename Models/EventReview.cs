namespace Eventify.Models;

public class EventReview
{
    public int Id { get; set; }
    public int EventItemId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;

    public EventItem? EventItem { get; set; }
}
