using Eventify.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
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
                Message = "Payment of $299 for \"Business Conference\" processed successfully.",
                Kind = "payment",
                IsRead = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
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
}

app.Run();
