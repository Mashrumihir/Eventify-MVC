using Eventify.Models;

namespace Eventify.ViewModels;

public class OrganizerManageEventsViewModel
{
    public string Search { get; set; } = string.Empty;
    public List<OrganizerManageEventCardViewModel> Events { get; set; } = new();
}

public class OrganizerManageEventCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "Upcoming";
    public decimal Revenue { get; set; }
    public int Sold { get; set; }
    public int Capacity { get; set; }
}

public class OrganizerEventDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "Upcoming";
    public decimal Revenue { get; set; }
    public int Sold { get; set; }
    public int Capacity { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class OrganizerBookingsViewModel
{
    public string Search { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? EventId { get; set; }
    public List<EventItem> Events { get; set; } = new();
    public List<OrganizerBookingRowViewModel> Rows { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class OrganizerBookingRowViewModel
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string AttendeeName { get; set; } = string.Empty;
    public string AttendeeEmail { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string TicketType { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

public class OrganizerPaymentsViewModel
{
    public decimal TotalRevenue { get; set; }
    public decimal PendingPayouts { get; set; }
    public int SuccessfulTransactions { get; set; }
    public int RefundRequests { get; set; }
    public List<OrganizerPaymentRowViewModel> Rows { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class OrganizerPaymentRowViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class OrganizerCouponsViewModel
{
    public string Code { get; set; } = string.Empty;
    public decimal DiscountPercentage { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int UsageLimit { get; set; }
    public int UsageCount { get; set; }
    public bool IsActive { get; set; } = true;
    public List<OrganizerCoupon> Coupons { get; set; } = new();
}

public class OrganizerAnnouncementsViewModel
{
    public string Title { get; set; } = string.Empty;
    public int? EventItemId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<EventItem> Events { get; set; } = new();
    public List<OrganizerAnnouncementListItemViewModel> Announcements { get; set; } = new();
}

public class OrganizerAnnouncementListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class OrganizerCreateEventViewModel
{
    public int? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string VenueAddress { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string TicketType { get; set; } = "Free";
    public decimal TicketPrice { get; set; }
    public int AvailableQuantity { get; set; }
    public bool EarlyBirdDiscount { get; set; }
    public decimal EarlyBirdPrice { get; set; }
    public string RefundPolicy { get; set; } = string.Empty;
}

public class OrganizerProfileSettingsViewModel
{
    public string ActiveTab { get; set; } = "edit";
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ProfilePhotoPath { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Bio { get; set; } = string.Empty;

    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
    public bool EventReminders { get; set; } = true;
    public bool PromotionsOffers { get; set; }

    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
