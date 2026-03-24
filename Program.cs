using Eventify.Data;
using Eventify.Utilities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddDbContext<EventifyDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EventifyDbContext>();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER NOT NULL CONSTRAINT PK_Users PRIMARY KEY AUTOINCREMENT,
            FullName TEXT NOT NULL,
            Email TEXT NOT NULL,
            PasswordHash TEXT NOT NULL,
            Role TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Email ON Users (Email);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS Bookings (
            Id INTEGER NOT NULL CONSTRAINT PK_Bookings PRIMARY KEY AUTOINCREMENT,
            EventItemId INTEGER NOT NULL,
            UserEmail TEXT NOT NULL,
            Status TEXT NOT NULL,
            IsSaved INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE INDEX IF NOT EXISTS IX_Bookings_EventItemId ON Bookings (EventItemId);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS MyBookings (
            Id INTEGER NOT NULL CONSTRAINT PK_MyBookings PRIMARY KEY AUTOINCREMENT,
            BookingCode TEXT NOT NULL,
            EventItemId INTEGER NOT NULL,
            UserEmail TEXT NOT NULL,
            TicketName TEXT NOT NULL,
            Quantity INTEGER NOT NULL,
            UnitPrice REAL NOT NULL,
            TotalAmount REAL NOT NULL,
            Status TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE INDEX IF NOT EXISTS IX_MyBookings_EventItemId ON MyBookings (EventItemId);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE INDEX IF NOT EXISTS IX_MyBookings_UserEmail ON MyBookings (UserEmail);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AttendNotifications (
            Id INTEGER NOT NULL CONSTRAINT PK_AttendNotifications PRIMARY KEY AUTOINCREMENT,
            UserEmail TEXT NOT NULL,
            Title TEXT NOT NULL,
            Message TEXT NOT NULL,
            Kind TEXT NOT NULL,
            IsRead INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE INDEX IF NOT EXISTS IX_AttendNotifications_UserEmail ON AttendNotifications (UserEmail);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AttendReviews (
            Id INTEGER NOT NULL CONSTRAINT PK_AttendReviews PRIMARY KEY AUTOINCREMENT,
            EventItemId INTEGER NOT NULL,
            UserEmail TEXT NOT NULL,
            Rating INTEGER NOT NULL,
            Comment TEXT NOT NULL,
            ReviewedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE INDEX IF NOT EXISTS IX_AttendReviews_UserEmail ON AttendReviews (UserEmail);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AttendProfileSettings (
            Id INTEGER NOT NULL CONSTRAINT PK_AttendProfileSettings PRIMARY KEY AUTOINCREMENT,
            UserEmail TEXT NOT NULL,
            ProfilePhotoPath TEXT NOT NULL,
            PhoneNumber TEXT NOT NULL,
            Location TEXT NOT NULL,
            DateOfBirth TEXT NULL,
            Bio TEXT NOT NULL,
            EmailNotifications INTEGER NOT NULL,
            PushNotifications INTEGER NOT NULL,
            EventReminders INTEGER NOT NULL,
            PromotionsOffers INTEGER NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_AttendProfileSettings_UserEmail ON AttendProfileSettings (UserEmail);
        """);
    try
    {
        db.Database.ExecuteSqlRaw(
            """
            ALTER TABLE AttendProfileSettings ADD COLUMN ProfilePhotoPath TEXT NOT NULL DEFAULT '';
            """);
    }
    catch
    {
    }
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS OrganizerCoupons (
            Id INTEGER NOT NULL CONSTRAINT PK_OrganizerCoupons PRIMARY KEY AUTOINCREMENT,
            Code TEXT NOT NULL,
            DiscountPercent REAL NOT NULL,
            ExpiryDate TEXT NOT NULL,
            UsageLimit INTEGER NOT NULL,
            UsageCount INTEGER NOT NULL,
            IsActive INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OrganizerCoupons_Code ON OrganizerCoupons (Code);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS OrganizerAnnouncements (
            Id INTEGER NOT NULL CONSTRAINT PK_OrganizerAnnouncements PRIMARY KEY AUTOINCREMENT,
            EventItemId INTEGER NULL,
            Title TEXT NOT NULL,
            Message TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS OrganizerEventConfigs (
            Id INTEGER NOT NULL CONSTRAINT PK_OrganizerEventConfigs PRIMARY KEY AUTOINCREMENT,
            EventItemId INTEGER NOT NULL,
            AvailableQuantity INTEGER NOT NULL,
            EarlyBirdDiscount INTEGER NOT NULL,
            EarlyBirdPrice REAL NOT NULL,
            RefundPolicy TEXT NOT NULL,
            GalleryImagesJson TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OrganizerEventConfigs_EventItemId ON OrganizerEventConfigs (EventItemId);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS OrganizerProfileSettings (
            Id INTEGER NOT NULL CONSTRAINT PK_OrganizerProfileSettings PRIMARY KEY AUTOINCREMENT,
            UserEmail TEXT NOT NULL,
            ProfilePhotoPath TEXT NOT NULL,
            PhoneNumber TEXT NOT NULL,
            Location TEXT NOT NULL,
            DateOfBirth TEXT NULL,
            Bio TEXT NOT NULL,
            EmailNotifications INTEGER NOT NULL,
            PushNotifications INTEGER NOT NULL,
            EventReminders INTEGER NOT NULL,
            PromotionsOffers INTEGER NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OrganizerProfileSettings_UserEmail ON OrganizerProfileSettings (UserEmail);
        """);
    try
    {
        db.Database.ExecuteSqlRaw(
            """
            ALTER TABLE OrganizerProfileSettings ADD COLUMN ProfilePhotoPath TEXT NOT NULL DEFAULT '';
            """);
    }
    catch
    {
    }
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS OrganizerPaymentRecords (
            Id INTEGER NOT NULL CONSTRAINT PK_OrganizerPaymentRecords PRIMARY KEY AUTOINCREMENT,
            MyBookingId INTEGER NOT NULL,
            TransactionId TEXT NOT NULL,
            Method TEXT NOT NULL,
            Status TEXT NOT NULL,
            PaidAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OrganizerPaymentRecords_TransactionId ON OrganizerPaymentRecords (TransactionId);
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AdminOrganizerApplications (
            Id INTEGER NOT NULL CONSTRAINT PK_AdminOrganizerApplications PRIMARY KEY AUTOINCREMENT,
            OrganizationName TEXT NOT NULL,
            Email TEXT NOT NULL,
            AppliedOnUtc TEXT NOT NULL,
            BusinessLicenseSubmitted INTEGER NOT NULL,
            TaxIdSubmitted INTEGER NOT NULL,
            IdVerificationSubmitted INTEGER NOT NULL,
            Status TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AdminModerationEvents (
            Id INTEGER NOT NULL CONSTRAINT PK_AdminModerationEvents PRIMARY KEY AUTOINCREMENT,
            EventItemId INTEGER NULL,
            EventTitle TEXT NOT NULL,
            OrganizerName TEXT NOT NULL,
            EventDate TEXT NOT NULL,
            Location TEXT NOT NULL,
            Capacity INTEGER NOT NULL,
            Price REAL NOT NULL,
            Status TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AdminCategories (
            Id INTEGER NOT NULL CONSTRAINT PK_AdminCategories PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT NOT NULL,
            Icon TEXT NOT NULL,
            EventCount INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AdminCmsPages (
            Id INTEGER NOT NULL CONSTRAINT PK_AdminCmsPages PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            Slug TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AdminReviewRatings (
            Id INTEGER NOT NULL CONSTRAINT PK_AdminReviewRatings PRIMARY KEY AUTOINCREMENT,
            ReviewerName TEXT NOT NULL,
            EventName TEXT NOT NULL,
            Rating INTEGER NOT NULL,
            Comment TEXT NOT NULL,
            Status TEXT NOT NULL,
            IsReported INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS AdminDashboardTrends (
            Id INTEGER NOT NULL CONSTRAINT PK_AdminDashboardTrends PRIMARY KEY AUTOINCREMENT,
            Metric TEXT NOT NULL,
            PercentChange REAL NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_AdminDashboardTrends_Metric ON AdminDashboardTrends (Metric);
        """);
    db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS AdminReviewModerations;");
    DbInitializer.Seed(db);

    if (!db.Bookings.Any() && db.Events.Any())
    {
        var eventIds = db.Events.Select(e => e.Id).ToList();
        var demoBookings = new List<Eventify.Models.Booking>();
        for (var i = 0; i < 24; i++)
        {
            demoBookings.Add(new Eventify.Models.Booking
            {
                EventItemId = eventIds[i % eventIds.Count],
                UserEmail = "attend@eventify.com",
                Status = i % 10 == 0 ? "Canceled" : "Booked",
                IsSaved = i % 2 == 0
            });
        }

        db.Bookings.AddRange(demoBookings);
        db.SaveChanges();
    }

    if (!db.MyBookings.Any() && db.Bookings.Any())
    {
        var existingEvents = db.Events.ToDictionary(e => e.Id);
        var myBookings = db.Bookings
            .Where(b => !b.IsSaved)
            .OrderBy(b => b.Id)
            .AsEnumerable()
            .Select(b =>
            {
                var hasEvent = existingEvents.TryGetValue(b.EventItemId, out var ev);
                var price = hasEvent ? ev!.Price : 0m;
                return new Eventify.Models.MyBooking
                {
                    BookingCode = $"BK{b.Id:000000}",
                    EventItemId = b.EventItemId,
                    UserEmail = string.IsNullOrWhiteSpace(b.UserEmail) ? "attend@eventify.com" : b.UserEmail,
                    TicketName = "Regular",
                    Quantity = 1,
                    UnitPrice = price,
                    TotalAmount = price,
                    Status = b.Status,
                    CreatedAtUtc = b.CreatedAtUtc
                };
            })
            .ToList();

        if (myBookings.Count > 0)
        {
            db.MyBookings.AddRange(myBookings);
            db.SaveChanges();
        }
    }

    if (!db.AttendNotifications.Any())
    {
        db.AttendNotifications.AddRange(
            new Eventify.Models.AttendNotification
            {
                UserEmail = "attend@eventify.com",
                Title = "Booking Confirmed",
                Message = "Your ticket for \"Tech Conference 2024\" has been confirmed.",
                Kind = "booking",
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
            },
            new Eventify.Models.AttendNotification
            {
                UserEmail = "attend@eventify.com",
                Title = "Event Reminder",
                Message = "\"Digital Marketing Summit\" starts in 2 days.",
                Kind = "reminder",
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-5)
            },
            new Eventify.Models.AttendNotification
            {
                UserEmail = "attend@eventify.com",
                Title = "Wishlist Event Update",
                Message = "New dates available for \"Music Festival 2024\".",
                Kind = "wishlist",
                IsRead = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new Eventify.Models.AttendNotification
            {
                UserEmail = "attend@eventify.com",
                Title = "Leave a Review",
                Message = "How was \"Web Development Workshop\"? Share your experience.",
                Kind = "review",
                IsRead = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new Eventify.Models.AttendNotification
            {
                UserEmail = "attend@eventify.com",
                Title = "Payment Successful",
                Message = "Payment of ₹299 for \"Business Conference\" processed successfully.",
                Kind = "payment",
                IsRead = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
            },
            new Eventify.Models.AttendNotification
            {
                UserEmail = "attend@eventify.com",
                Title = "Event Cancellation",
                Message = "Unfortunately, \"Workshop Series\" has been cancelled. Refund will be processed.",
                Kind = "cancellation",
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-6)
            }
        );
        db.SaveChanges();
    }

    if (!db.AttendReviews.Any() && db.Events.Any())
    {
        var eventIds = db.Events.Select(e => e.Id).Take(3).ToList();
        if (eventIds.Count > 0)
        {
            var seedReviews = new List<Eventify.Models.AttendReview>();
            for (var i = 0; i < eventIds.Count; i++)
            {
                seedReviews.Add(new Eventify.Models.AttendReview
                {
                    EventItemId = eventIds[i],
                    UserEmail = "attend@eventify.com",
                    Rating = i == 1 ? 4 : 5,
                    Comment = i switch
                    {
                        0 => "Amazing event! Great speakers and strong networking opportunities.",
                        1 => "Great lineup and atmosphere. Would attend again.",
                        _ => "Excellent experience and very well organized."
                    },
                    ReviewedAtUtc = DateTime.UtcNow.AddDays(-(i + 1))
                });
            }

            db.AttendReviews.AddRange(seedReviews);
            db.SaveChanges();
        }
    }

    if (!db.AttendProfileSettings.Any(s => s.UserEmail == "attend@eventify.com"))
    {
        db.AttendProfileSettings.Add(new Eventify.Models.AttendProfileSetting
        {
            UserEmail = "attend@eventify.com",
            PhoneNumber = "+91 90000 00000",
            Location = "Rajkot, Gujarat",
            Bio = "I enjoy attending technology and cultural events."
        });
        db.SaveChanges();
    }

    if (db.OrganizerCoupons.Any())
    {
        db.OrganizerCoupons.RemoveRange(db.OrganizerCoupons);
        db.SaveChanges();
    }

    if (!db.OrganizerAnnouncements.Any())
    {
        var events = db.Events.OrderBy(e => e.Id).Take(3).ToList();
        db.OrganizerAnnouncements.AddRange(
            new Eventify.Models.OrganizerAnnouncement
            {
                EventItemId = events.Count > 1 ? events[1].Id : null,
                Title = "Venue Change for Summer Festival",
                Message = "Due to weather conditions, the venue has been moved to Indoor Arena. All ticket holders will receive updated location details via email. The event timing remains the same.",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new Eventify.Models.OrganizerAnnouncement
            {
                EventItemId = events.Count > 0 ? events[0].Id : null,
                Title = "Early Bird Tickets Available",
                Message = "Get 20% off on tickets purchased before March 1st! Limited time offer for our most anticipated technology conference of the year. Secure your spot today and save big.",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-4)
            },
            new Eventify.Models.OrganizerAnnouncement
            {
                EventItemId = events.Count > 2 ? events[2].Id : null,
                Title = "New VIP Package Launched",
                Message = "Exclusive VIP packages now available with backstage access and premium seating. Includes meet and greet with speakers, premium networking lounge access, and complimentary refreshments.",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-6)
            }
        );
        db.SaveChanges();
    }

    if (!db.OrganizerPaymentRecords.Any())
    {
        var bookings = db.MyBookings
            .Where(b => b.Status == "Booked")
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(12)
            .ToList();

        var methods = new[] { "Credit Card", "UPI", "Net Banking", "Debit Card" };
        for (var i = 0; i < bookings.Count; i++)
        {
            db.OrganizerPaymentRecords.Add(new Eventify.Models.OrganizerPaymentRecord
            {
                MyBookingId = bookings[i].Id,
                TransactionId = $"TXN{i + 1234:000000}",
                Method = methods[i % methods.Length],
                Status = "Success",
                PaidAtUtc = bookings[i].CreatedAtUtc
            });
        }

        db.SaveChanges();
    }

    if (!db.AdminOrganizerApplications.Any())
    {
        db.AdminOrganizerApplications.AddRange(
            new Eventify.Models.AdminOrganizerApplication
            {
                OrganizationName = "Tech Events Inc",
                Email = "contact@techevents.com",
                AppliedOnUtc = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                BusinessLicenseSubmitted = true,
                TaxIdSubmitted = true,
                IdVerificationSubmitted = true,
                Status = "pending"
            },
            new Eventify.Models.AdminOrganizerApplication
            {
                OrganizationName = "Music Festival Co",
                Email = "info@musicfest.com",
                AppliedOnUtc = new DateTime(2024, 1, 18, 11, 30, 0, DateTimeKind.Utc),
                BusinessLicenseSubmitted = true,
                TaxIdSubmitted = false,
                IdVerificationSubmitted = true,
                Status = "pending"
            },
            new Eventify.Models.AdminOrganizerApplication
            {
                OrganizationName = "Creative Minds Agency",
                Email = "hello@creativeminds.com",
                AppliedOnUtc = new DateTime(2024, 1, 10, 9, 0, 0, DateTimeKind.Utc),
                BusinessLicenseSubmitted = true,
                TaxIdSubmitted = true,
                IdVerificationSubmitted = true,
                Status = "approved"
            },
            new Eventify.Models.AdminOrganizerApplication
            {
                OrganizationName = "Night Owl Events",
                Email = "admin@nightowl.io",
                AppliedOnUtc = new DateTime(2024, 1, 9, 13, 0, 0, DateTimeKind.Utc),
                BusinessLicenseSubmitted = false,
                TaxIdSubmitted = false,
                IdVerificationSubmitted = true,
                Status = "rejected"
            }
        );
        db.SaveChanges();
    }

    if (!db.AdminModerationEvents.Any())
    {
        var seededEvents = db.Events
            .OrderBy(e => e.Id)
            .Take(4)
            .ToList();

        db.AdminModerationEvents.AddRange(
            new Eventify.Models.AdminEventModeration
            {
                EventItemId = seededEvents.Count > 0 ? seededEvents[0].Id : null,
                EventTitle = seededEvents.Count > 0 ? seededEvents[0].Title : "Summer Festival 2024",
                OrganizerName = "Tech Events Inc",
                EventDate = seededEvents.Count > 0 ? seededEvents[0].StartDateTime : new DateTime(2026, 6, 15),
                Location = seededEvents.Count > 0 ? seededEvents[0].Location : "Rajkot",
                Capacity = seededEvents.Count > 0 ? Math.Max(100, seededEvents[0].AttendingCount) : 500,
                Price = seededEvents.Count > 0 ? seededEvents[0].Price : 499m,
                Status = "pending",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new Eventify.Models.AdminEventModeration
            {
                EventItemId = seededEvents.Count > 1 ? seededEvents[1].Id : null,
                EventTitle = seededEvents.Count > 1 ? seededEvents[1].Title : "Music Nights Live",
                OrganizerName = "Music Festival Co",
                EventDate = seededEvents.Count > 1 ? seededEvents[1].StartDateTime : new DateTime(2026, 7, 5),
                Location = seededEvents.Count > 1 ? seededEvents[1].Location : "Ahmedabad",
                Capacity = seededEvents.Count > 1 ? Math.Max(100, seededEvents[1].AttendingCount) : 800,
                Price = seededEvents.Count > 1 ? seededEvents[1].Price : 699m,
                Status = "pending",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new Eventify.Models.AdminEventModeration
            {
                EventItemId = seededEvents.Count > 2 ? seededEvents[2].Id : null,
                EventTitle = seededEvents.Count > 2 ? seededEvents[2].Title : "Startup Summit",
                OrganizerName = "Creative Minds Agency",
                EventDate = seededEvents.Count > 2 ? seededEvents[2].StartDateTime : new DateTime(2026, 8, 20),
                Location = seededEvents.Count > 2 ? seededEvents[2].Location : "Surat",
                Capacity = seededEvents.Count > 2 ? Math.Max(100, seededEvents[2].AttendingCount) : 350,
                Price = seededEvents.Count > 2 ? seededEvents[2].Price : 399m,
                Status = "approved",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-6)
            },
            new Eventify.Models.AdminEventModeration
            {
                EventItemId = seededEvents.Count > 3 ? seededEvents[3].Id : null,
                EventTitle = seededEvents.Count > 3 ? seededEvents[3].Title : "Art Expo",
                OrganizerName = "Night Owl Events",
                EventDate = seededEvents.Count > 3 ? seededEvents[3].StartDateTime : new DateTime(2026, 9, 2),
                Location = seededEvents.Count > 3 ? seededEvents[3].Location : "Vadodara",
                Capacity = seededEvents.Count > 3 ? Math.Max(100, seededEvents[3].AttendingCount) : 250,
                Price = seededEvents.Count > 3 ? seededEvents[3].Price : 299m,
                Status = "rejected",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
            }
        );
        db.SaveChanges();
    }

    var currencyNotifications = db.AttendNotifications
        .Where(n => n.Message.Contains("$"))
        .ToList();
    if (currencyNotifications.Count > 0)
    {
        foreach (var notification in currencyNotifications)
        {
            notification.Message = notification.Message.Replace("$", "₹");
        }
        db.SaveChanges();
    }

    if (!db.AdminCategories.Any())
    {
        db.AdminCategories.AddRange(
            new Eventify.Models.AdminCategory { Name = "Technology", Description = "Tech conferences and workshops", Icon = "bi-pc-display", EventCount = 45 },
            new Eventify.Models.AdminCategory { Name = "Music", Description = "Concerts and music festivals", Icon = "bi-music-note-beamed", EventCount = 32 },
            new Eventify.Models.AdminCategory { Name = "Sports", Description = "Sports events and championships", Icon = "bi-dribbble", EventCount = 28 },
            new Eventify.Models.AdminCategory { Name = "Arts", Description = "Art exhibitions and cultural events", Icon = "bi-palette-fill", EventCount = 19 }
        );
        db.SaveChanges();
    }

    if (!db.AdminCmsPages.Any())
    {
        db.AdminCmsPages.AddRange(
            new Eventify.Models.AdminCmsPage { Title = "About Us", Slug = "/about", UpdatedAtUtc = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new Eventify.Models.AdminCmsPage { Title = "Privacy Policy", Slug = "/privacy", UpdatedAtUtc = new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
            new Eventify.Models.AdminCmsPage { Title = "Terms & Conditions", Slug = "/terms", UpdatedAtUtc = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Eventify.Models.AdminCmsPage { Title = "FAQ", Slug = "/faq", UpdatedAtUtc = new DateTime(2024, 1, 8, 0, 0, 0, DateTimeKind.Utc) }
        );
        db.SaveChanges();
    }

    if (!db.AdminReviewRatings.Any())
    {
        db.AdminReviewRatings.AddRange(
            new Eventify.Models.AdminReviewRating
            {
                ReviewerName = "Mihir",
                EventName = "Summer Music Festival",
                Rating = 5,
                Comment = "Good Music",
                Status = "approved",
                IsReported = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-21)
            },
            new Eventify.Models.AdminReviewRating
            {
                ReviewerName = "Emma Wilson",
                EventName = "Summer Music Festival",
                Rating = 4,
                Comment = "Good music festival but the sound system could be better.",
                Status = "approved",
                IsReported = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-21)
            },
            new Eventify.Models.AdminReviewRating
            {
                ReviewerName = "Sarah Davis",
                EventName = "Art Exhibition 2024",
                Rating = 5,
                Comment = "Absolutely stunning exhibition!",
                Status = "approved",
                IsReported = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-28)
            }
        );
        db.SaveChanges();
    }

    if (!db.AdminDashboardTrends.Any())
    {
        db.AdminDashboardTrends.AddRange(
            new Eventify.Models.AdminDashboardTrend { Metric = "users", PercentChange = 12.5m },
            new Eventify.Models.AdminDashboardTrend { Metric = "organizers", PercentChange = 8.2m },
            new Eventify.Models.AdminDashboardTrend { Metric = "events", PercentChange = 15.3m },
            new Eventify.Models.AdminDashboardTrend { Metric = "revenue", PercentChange = -3.1m }
        );
        db.SaveChanges();
    }

    var allUsers = db.Users.ToList();
    await RoleDatabaseMirror.SyncAllUsersAsync(builder.Configuration, allUsers);
}

app.Run();


