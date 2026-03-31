namespace Eventify.Models;

public class UserAccount
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string Role { get; set; } = "attend";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
