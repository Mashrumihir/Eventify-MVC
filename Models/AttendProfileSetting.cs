namespace Eventify.Models;

public class AttendProfileSetting
{
    public int Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string ProfilePhotoPath { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Bio { get; set; } = string.Empty;
    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
    public bool EventReminders { get; set; } = true;
    public bool PromotionsOffers { get; set; }
}
