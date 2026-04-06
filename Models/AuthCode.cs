namespace Eventify.Models;

public class AuthCode
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAtUtc { get; set; }
}
