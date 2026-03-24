namespace Eventify.Models;

public class AdminOrganizerApplication
{
    public int Id { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime AppliedOnUtc { get; set; } = DateTime.UtcNow;
    public bool BusinessLicenseSubmitted { get; set; }
    public bool TaxIdSubmitted { get; set; }
    public bool IdVerificationSubmitted { get; set; }
    public string Status { get; set; } = "pending";
}
