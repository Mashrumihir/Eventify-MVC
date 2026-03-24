namespace Eventify.Models;

public class AdminEventModeration
{
    public int Id { get; set; }
    public int? EventItemId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
