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
    public DbSet<OrganizerCoupon> OrganizerCoupons => Set<OrganizerCoupon>();
    public DbSet<OrganizerAnnouncement> OrganizerAnnouncements => Set<OrganizerAnnouncement>();
    public DbSet<OrganizerEventConfig> OrganizerEventConfigs => Set<OrganizerEventConfig>();
    public DbSet<OrganizerProfileSetting> OrganizerProfileSettings => Set<OrganizerProfileSetting>();
    public DbSet<OrganizerPaymentRecord> OrganizerPaymentRecords => Set<OrganizerPaymentRecord>();
    public DbSet<AdminOrganizerApplication> AdminOrganizerApplications => Set<AdminOrganizerApplication>();
    public DbSet<AdminEventModeration> AdminModerationEvents => Set<AdminEventModeration>();
    public DbSet<AdminCategory> AdminCategories => Set<AdminCategory>();
    public DbSet<AdminCmsPage> AdminCmsPages => Set<AdminCmsPage>();
    public DbSet<AdminReviewRating> AdminReviewRatings => Set<AdminReviewRating>();
    public DbSet<AdminDashboardTrend> AdminDashboardTrends => Set<AdminDashboardTrend>();
}
