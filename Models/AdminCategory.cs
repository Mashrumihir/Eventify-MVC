namespace Eventify.Models;

public class AdminCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-tag";
    public int EventCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
