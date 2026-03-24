namespace Eventify.ViewModels;

public class AttendNotificationsViewModel
{
    public int UnreadCount { get; set; }
    public List<AttendNotificationItemViewModel> Items { get; set; } = new();
}

public class AttendNotificationItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Kind { get; set; } = "info";
    public bool IsUnread { get; set; }
    public string RelativeTime { get; set; } = string.Empty;
}
