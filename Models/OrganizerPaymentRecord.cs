namespace Eventify.Models;

public class OrganizerPaymentRecord
{
    public int Id { get; set; }
    public int MyBookingId { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Method { get; set; } = "UPI";
    public string Status { get; set; } = "Success";
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

    public MyBooking? MyBooking { get; set; }
}
