namespace Eventify.Models;

public class AttendReview
{
    public int Id { get; set; }
    public int EventItemId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public string Comment { get; set; } = string.Empty;
    public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;
}
