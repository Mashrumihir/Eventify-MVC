namespace Eventify.ViewModels;

public class OrganizerDashboardViewModel
{
    public decimal TotalRevenue { get; set; }
    public int TicketsSold { get; set; }
    public int TotalEvents { get; set; }
    public decimal ConversionRate { get; set; }
    public decimal RevenueGrowth { get; set; }
    public decimal TicketsGrowth { get; set; }
    public decimal EventsGrowth { get; set; }
    public decimal ConversionGrowth { get; set; }

    public List<int> SalesSeries { get; set; } = new();
    public List<int> RevenueSeries { get; set; } = new();
    public List<int> VisitorsSeries { get; set; } = new();
    public List<string> DateLabels { get; set; } = new();

    public List<OrganizerActivityItemViewModel> RecentActivity { get; set; } = new();
}

public class OrganizerActivityItemViewModel
{
    public string EventName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ActionText { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
}
