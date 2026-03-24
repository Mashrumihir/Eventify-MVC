namespace Eventify.Models;

public class AdminReviewRating
{
    public int Id { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public string Comment { get; set; } = string.Empty;
    public string Status { get; set; } = "approved";
    public bool IsReported { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
