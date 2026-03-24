using Eventify.Models;

namespace Eventify.ViewModels;

public class AttendDashboardViewModel
{
    public string UserDisplayName { get; set; } = "Attend User";
    public int TotalBookings { get; set; }
    public int UpcomingCount { get; set; }
    public int CanceledCount { get; set; }
    public int SavedCount { get; set; }
    public List<EventItem> UpcomingEvents { get; set; } = new();
    public List<EventItem> RecommendedEvents { get; set; } = new();
}
