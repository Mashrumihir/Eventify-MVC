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
    public DbSet<AuthCode> AuthCodes => Set<AuthCode>();
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
    public DbSet<AdminReviewRating> AdminReviewRatings => Set<AdminReviewRating>();
    public DbSet<AdminDashboardTrend> AdminDashboardTrends => Set<AdminDashboardTrend>();
    public DbSet<AdminNewsletterSubscriber> AdminNewsletterSubscribers => Set<AdminNewsletterSubscriber>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAccount>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<AuthCode>()
            .Property(code => code.Email)
            .HasMaxLength(256);

        modelBuilder.Entity<AuthCode>()
            .Property(code => code.Purpose)
            .HasMaxLength(50);

        modelBuilder.Entity<AuthCode>()
            .Property(code => code.Code)
            .HasMaxLength(6);

        modelBuilder.Entity<AuthCode>()
            .HasIndex(code => new { code.Email, code.Purpose, code.Code });

        modelBuilder.Entity<EventItem>()
            .Property(item => item.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Booking>()
            .HasOne(booking => booking.EventItem)
            .WithMany()
            .HasForeignKey(booking => booking.EventItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MyBooking>()
            .HasOne(booking => booking.EventItem)
            .WithMany()
            .HasForeignKey(booking => booking.EventItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MyBooking>()
            .Property(booking => booking.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<MyBooking>()
            .Property(booking => booking.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<MyBooking>()
            .HasIndex(booking => booking.UserEmail);

        modelBuilder.Entity<AttendNotification>()
            .HasIndex(notification => notification.UserEmail);

        modelBuilder.Entity<AttendReview>()
            .HasIndex(review => review.UserEmail);

        modelBuilder.Entity<AttendProfileSetting>()
            .HasIndex(profile => profile.UserEmail)
            .IsUnique();

        modelBuilder.Entity<OrganizerCoupon>()
            .Property(coupon => coupon.DiscountPercent)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OrganizerCoupon>()
            .HasIndex(coupon => coupon.Code)
            .IsUnique();

        modelBuilder.Entity<OrganizerAnnouncement>()
            .HasOne(announcement => announcement.EventItem)
            .WithMany()
            .HasForeignKey(announcement => announcement.EventItemId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OrganizerEventConfig>()
            .Property(config => config.EarlyBirdPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OrganizerEventConfig>()
            .HasIndex(config => config.EventItemId)
            .IsUnique();

        modelBuilder.Entity<OrganizerProfileSetting>()
            .HasIndex(profile => profile.UserEmail)
            .IsUnique();

        modelBuilder.Entity<OrganizerPaymentRecord>()
            .HasOne(payment => payment.MyBooking)
            .WithMany()
            .HasForeignKey(payment => payment.MyBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrganizerPaymentRecord>()
            .HasIndex(payment => payment.TransactionId)
            .IsUnique();

        modelBuilder.Entity<AdminDashboardTrend>()
            .Property(trend => trend.PercentChange)
            .HasPrecision(18, 2);

        modelBuilder.Entity<AdminDashboardTrend>()
            .HasIndex(trend => trend.Metric)
            .IsUnique();

        modelBuilder.Entity<AdminNewsletterSubscriber>()
            .Property(subscriber => subscriber.Email)
            .HasMaxLength(256);

        modelBuilder.Entity<AdminNewsletterSubscriber>()
            .HasIndex(subscriber => subscriber.Email)
            .IsUnique();

        modelBuilder.Entity<AdminEventModeration>()
            .Property(item => item.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<TicketOption>()
            .Property(option => option.Price)
            .HasPrecision(18, 2);
    }
}
