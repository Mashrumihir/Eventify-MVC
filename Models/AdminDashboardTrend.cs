namespace Eventify.Models;

public class AdminDashboardTrend
{
    public int Id { get; set; }
    public string Metric { get; set; } = string.Empty;
    public decimal PercentChange { get; set; }
}
