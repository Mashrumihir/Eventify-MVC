using System.ComponentModel.DataAnnotations;

namespace Eventify.ViewModels;

public class AttendProfileSettingsViewModel
{
    public string ActiveTab { get; set; } = "edit";
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }
    public string Bio { get; set; } = string.Empty;

    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
    public bool EventReminders { get; set; } = true;
    public bool PromotionsOffers { get; set; }

    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
