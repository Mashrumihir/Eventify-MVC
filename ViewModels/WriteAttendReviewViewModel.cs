using System.ComponentModel.DataAnnotations;

namespace Eventify.ViewModels;

public class WriteAttendReviewViewModel
{
    public int? ReviewId { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [Required]
    [StringLength(1200)]
    public string Comment { get; set; } = string.Empty;
}
