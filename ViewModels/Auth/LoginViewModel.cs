using System.ComponentModel.DataAnnotations;

namespace Eventify.ViewModels.Auth;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "attend";

    public bool RememberMe { get; set; }
}
