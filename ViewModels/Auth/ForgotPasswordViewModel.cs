using System.ComponentModel.DataAnnotations;

namespace Eventify.ViewModels.Auth;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
