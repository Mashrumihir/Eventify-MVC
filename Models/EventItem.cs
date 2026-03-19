namespace Eventify.Models;

public class EventItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public int AttendingCount { get; set; }
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<TicketOption> TicketOptions { get; set; } = new List<TicketOption>();
    public ICollection<EventSpeaker> Speakers { get; set; } = new List<EventSpeaker>();
    public ICollection<EventReview> Reviews { get; set; } = new List<EventReview>();
}
