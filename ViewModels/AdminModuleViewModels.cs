using Eventify.Models;

namespace Eventify.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalOrganizers { get; set; }
    public int TotalEvents { get; set; }
    public decimal Revenue { get; set; }
    public decimal UsersTrendPercent { get; set; }
    public decimal OrganizersTrendPercent { get; set; }
    public decimal EventsTrendPercent { get; set; }
    public decimal RevenueTrendPercent { get; set; }
    public int NewsletterSubscribersCount { get; set; }
    public List<AdminPendingApprovalItemViewModel> PendingApprovals { get; set; } = new();
    public List<AdminNewsletterSubscriberItemViewModel> NewsletterSubscribers { get; set; } = new();
}

public class AdminNewsletterSubscriberItemViewModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string AddedOnText { get; set; } = string.Empty;
}

public class AdminNewsletterSubscribersViewModel
{
    public int TotalSubscribers { get; set; }
    public List<AdminNewsletterSubscriberItemViewModel> Subscribers { get; set; } = new();
}

public class AdminPendingApprovalItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
}

public class AdminUserManagementViewModel
{
    public string Search { get; set; } = string.Empty;
    public string ActiveRole { get; set; } = "all";
    public int AttendCount { get; set; }
    public int OrganizerCount { get; set; }
    public int AdminCount { get; set; }
    public List<AdminUserRowViewModel> Users { get; set; } = new();
}

public class AdminUserRowViewModel
{
    public int Id { get; set; }
    public string Initials { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string Role { get; set; } = "attend";
    public string Status { get; set; } = "active";
    public DateTime JoinDate { get; set; }
    public int Bookings { get; set; }
}

public class AdminOrganizerApprovalViewModel
{
    public string ActiveTab { get; set; } = "pending";
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public List<AdminOrganizerApplicationCardViewModel> Applications { get; set; } = new();
}

public class AdminOrganizerApplicationCardViewModel
{
    public int Id { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime AppliedOn { get; set; }
    public bool BusinessLicenseSubmitted { get; set; }
    public bool TaxIdSubmitted { get; set; }
    public bool IdVerificationSubmitted { get; set; }
    public string Status { get; set; } = "pending";
}

public class AdminEventModerationViewModel
{
    public string ActiveTab { get; set; } = "pending";
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int FeaturedCount { get; set; }
    public int RejectedCount { get; set; }
    public List<AdminEventModerationCardViewModel> Events { get; set; } = new();
}

public class AdminEventModerationCardViewModel
{
    public int Id { get; set; }
    public string Initials { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "pending";
}

public class AdminSystemSettingsViewModel
{
    public List<AdminCategory> Categories { get; set; } = new();
}

public class AdminNotificationsViewModel
{
    public List<AdminNotificationItemViewModel> Items { get; set; } = new();

    public int BookingsCount => Items.Count(x => x.Type == "booking");
    public int RemindersCount => Items.Count(x => x.Type == "reminder");
    public int PaymentsCount => Items.Count(x => x.Type == "payment");
    public int CancellationsCount => Items.Count(x => x.Type == "cancellation");
    public int AnnouncementsCount => Items.Count(x => x.Type == "announcement");
}

public class AdminNotificationItemViewModel
{
    public string Type { get; set; } = "announcement";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
    public DateTime SortDateUtc { get; set; } = DateTime.UtcNow;
}

public class AdminReviewsRatingsViewModel
{
    public List<AdminReviewRatingItemViewModel> Items { get; set; } = new();
    public int TotalReviews => Items.Count;
    public int ApprovedCount => Items.Count(x => x.Status == "approved");
    public int PendingCount => Items.Count(x => x.Status == "pending");
    public int ReportedCount => Items.Count(x => x.IsReported || x.Status == "reported");
}

public class AdminReviewRatingItemViewModel
{
    public int Id { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string ReviewerInitials { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string Status { get; set; } = "approved";
    public bool IsReported { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public DateTime SortDateUtc { get; set; } = DateTime.UtcNow;
}
