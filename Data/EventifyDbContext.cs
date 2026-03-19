using Eventify.Models;
using Microsoft.EntityFrameworkCore;

namespace Eventify.Data;

public class EventifyDbContext(DbContextOptions<EventifyDbContext> options) : DbContext(options)
{
    public DbSet<EventItem> Events => Set<EventItem>();
    public DbSet<TicketOption> TicketOptions => Set<TicketOption>();
    public DbSet<EventSpeaker> Speakers => Set<EventSpeaker>();
    public DbSet<EventReview> Reviews => Set<EventReview>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<MyBooking> MyBookings => Set<MyBooking>();
    public DbSet<AttendNotification> AttendNotifications => Set<AttendNotification>();
    public DbSet<AttendReview> AttendReviews => Set<AttendReview>();
    public DbSet<AttendProfileSetting> AttendProfileSettings => Set<AttendProfileSetting>();
}
