using System.ComponentModel.DataAnnotations;

namespace Eventify.ViewModels.Auth;

public class VerifyEmailViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Purpose { get; set; } = "verify-email";

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;

    public int CountdownSeconds { get; set; } = 60;
    public string DemoCode { get; set; } = string.Empty;
}
