namespace Eventify.ViewModels;

public class AttendReviewsViewModel
{
    public List<AttendReviewItemViewModel> Reviews { get; set; } = new();
    public List<AttendPendingReviewItemViewModel> Pending { get; set; } = new();
}

public class AttendReviewItemViewModel
{
    public int ReviewId { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }
}

public class AttendPendingReviewItemViewModel
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime AttendedOn { get; set; }
}
