using Eventify.Data;
using Eventify.Models.Email;
using Eventify.Utilities;
using Eventify.Utilities.Email;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<SmtpEmailSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddDbContext<EventifyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
    await db.Database.MigrateAsync();
    db.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[dbo].[OrganizerCoupons]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[OrganizerCoupons] (
                [Id] int NOT NULL IDENTITY,
                [Code] nvarchar(450) NOT NULL,
                [DiscountPercent] decimal(18,2) NOT NULL,
                [ExpiryDate] datetime2 NOT NULL,
                [UsageLimit] int NOT NULL,
                [UsageCount] int NOT NULL,
                [IsActive] bit NOT NULL,
                [CreatedAtUtc] datetime2 NOT NULL,
                CONSTRAINT [PK_OrganizerCoupons] PRIMARY KEY ([Id])
            );
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_OrganizerCoupons_Code'
              AND object_id = OBJECT_ID(N'[dbo].[OrganizerCoupons]')
        )
        BEGIN
            CREATE UNIQUE INDEX [IX_OrganizerCoupons_Code] ON [dbo].[OrganizerCoupons] ([Code]);
        END;
        """);
    db.Database.ExecuteSqlRaw(
        """
        IF COL_LENGTH('dbo.Users', 'IsEmailVerified') IS NULL
        BEGIN
            ALTER TABLE [dbo].[Users] ADD [IsEmailVerified] bit NOT NULL CONSTRAINT [DF_Users_IsEmailVerified] DEFAULT(0);
        END;

        IF COL_LENGTH('dbo.Users', 'EmailVerifiedAtUtc') IS NULL
        BEGIN
            ALTER TABLE [dbo].[Users] ADD [EmailVerifiedAtUtc] datetime2 NULL;
        END;

        IF COL_LENGTH('dbo.Users', 'PasswordChangedAtUtc') IS NULL
        BEGIN
            ALTER TABLE [dbo].[Users] ADD [PasswordChangedAtUtc] datetime2 NULL;
        END;

        UPDATE [dbo].[Users]
        SET [IsEmailVerified] = 1,
            [EmailVerifiedAtUtc] = ISNULL([EmailVerifiedAtUtc], SYSUTCDATETIME())
        WHERE [IsEmailVerified] = 0;

        UPDATE [dbo].[Users]
        SET [PasswordChangedAtUtc] = ISNULL([PasswordChangedAtUtc], [CreatedAtUtc])
        WHERE [PasswordChangedAtUtc] IS NULL;

        IF OBJECT_ID(N'[dbo].[AuthCodes]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[AuthCodes] (
                [Id] int NOT NULL IDENTITY,
                [Email] nvarchar(450) NOT NULL,
                [Purpose] nvarchar(450) NOT NULL,
                [Code] nvarchar(450) NOT NULL,
                [ExpiresAtUtc] datetime2 NOT NULL,
                [IsUsed] bit NOT NULL,
                [CreatedAtUtc] datetime2 NOT NULL,
                [UsedAtUtc] datetime2 NULL,
                CONSTRAINT [PK_AuthCodes] PRIMARY KEY ([Id])
            );
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_AuthCodes_Email_Purpose_Code'
              AND object_id = OBJECT_ID(N'[dbo].[AuthCodes]')
        )
        BEGIN
            CREATE INDEX [IX_AuthCodes_Email_Purpose_Code] ON [dbo].[AuthCodes] ([Email], [Purpose], [Code]);
        END;
        """);
    db.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[dbo].[AdminNewsletterSubscribers]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[AdminNewsletterSubscribers] (
                [Id] int NOT NULL IDENTITY,
                [Email] nvarchar(256) NOT NULL,
                [CreatedAtUtc] datetime2 NOT NULL,
                CONSTRAINT [PK_AdminNewsletterSubscribers] PRIMARY KEY ([Id])
            );
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_AdminNewsletterSubscribers_Email'
              AND object_id = OBJECT_ID(N'[dbo].[AdminNewsletterSubscribers]')
        )
        BEGIN
            CREATE UNIQUE INDEX [IX_AdminNewsletterSubscribers_Email] ON [dbo].[AdminNewsletterSubscribers] ([Email]);
        END;
        """);
    db.Database.ExecuteSqlRaw(
        """
        IF OBJECT_ID(N'[dbo].[AdminContactMessages]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[AdminContactMessages] (
                [Id] int NOT NULL IDENTITY,
                [FullName] nvarchar(120) NOT NULL,
                [Email] nvarchar(256) NOT NULL,
                [Subject] nvarchar(120) NOT NULL,
                [Message] nvarchar(max) NOT NULL,
                [CreatedAtUtc] datetime2 NOT NULL,
                CONSTRAINT [PK_AdminContactMessages] PRIMARY KEY ([Id])
            );
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N'IX_AdminContactMessages_CreatedAtUtc'
              AND object_id = OBJECT_ID(N'[dbo].[AdminContactMessages]')
        )
        BEGIN
            CREATE INDEX [IX_AdminContactMessages_CreatedAtUtc] ON [dbo].[AdminContactMessages] ([CreatedAtUtc]);
        END;
        """);
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

    if (!db.AdminNewsletterSubscribers.Any())
    {
        db.AdminNewsletterSubscribers.AddRange(
            new Eventify.Models.AdminNewsletterSubscriber
            {
                Email = "events@eventify.com",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
            },
            new Eventify.Models.AdminNewsletterSubscriber
            {
                Email = "offers@eventify.com",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
            },
            new Eventify.Models.AdminNewsletterSubscriber
            {
                Email = "updates@eventify.com",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            }
        );
        db.SaveChanges();
    }

    var allUsers = db.Users.ToList();
    await RoleDatabaseMirror.SyncAllUsersAsync(builder.Configuration, allUsers);
}

app.Run();


