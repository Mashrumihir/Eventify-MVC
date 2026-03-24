namespace Eventify.ViewModels;

public class AttendPaymentsViewModel
{
    public decimal TotalPaid { get; set; }
    public int SuccessfulTransactions { get; set; }
    public int FreeBookings { get; set; }
    public List<AttendPaymentRowViewModel> Rows { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class AttendPaymentRowViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string TicketName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BookingCode { get; set; } = string.Empty;
}
