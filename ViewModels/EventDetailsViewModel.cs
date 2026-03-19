using Eventify.Models;

namespace Eventify.ViewModels;

public class EventDetailsViewModel
{
    public EventItem Event { get; set; } = new();
    public List<TicketOption> TicketOptions { get; set; } = new();
    public List<EventSpeaker> Speakers { get; set; } = new();
    public List<EventReview> Reviews { get; set; } = new();
}
