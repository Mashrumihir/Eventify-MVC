using Eventify.Data;
using Eventify.Models;
using Eventify.ViewModels;
using Eventify.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Globalization;

namespace Eventify.Controllers;

public class AttendController(EventifyDbContext db) : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Dashboard()
    {
        ViewData["Title"] = "Dashboard";
        ViewData["AttendNav"] = "dashboard";

        var upcomingEvents = await db.Bookings
            .Where(b => b.Status == "Booked")
            .Join(db.Events, b => b.EventItemId, e => e.Id, (b, e) => e)
            .Where(e => e.StartDateTime >= DateTime.Today)
            .Distinct()
            .OrderBy(e => e.StartDateTime)
            .Take(3)
            .ToListAsync();

        if (upcomingEvents.Count == 0)
        {
            upcomingEvents = await db.Events
                .OrderBy(e => e.StartDateTime)
                .Take(3)
                .ToListAsync();
        }

        var recommendedEvents = await db.Events
            .OrderByDescending(e => e.Rating)
            .Take(3)
            .ToListAsync();

        var model = new AttendDashboardViewModel
        {
            TotalBookings = await db.Bookings.CountAsync(b => b.Status == "Booked"),
            UpcomingCount = await db.Bookings
                .Where(b => b.Status == "Booked")
                .Join(db.Events, b => b.EventItemId, e => e.Id, (b, e) => e)
                .CountAsync(e => e.StartDateTime >= DateTime.Today),
            CanceledCount = await db.Bookings.CountAsync(b => b.Status == "Canceled"),
            SavedCount = await db.Bookings.CountAsync(b => b.IsSaved),
            UpcomingEvents = upcomingEvents,
            RecommendedEvents = recommendedEvents
        };

        return View(model);
    }

    public async Task<IActionResult> BrowseEvents()
    {
        ViewData["Title"] = "Browse Events";
        ViewData["AttendNav"] = "browse";

        var events = await db.Events
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();

        var savedEventIds = await db.Bookings
            .Where(b => b.UserEmail == "attend@eventify.com" && b.IsSaved)
            .Select(b => b.EventItemId)
            .Distinct()
            .ToListAsync();

        ViewBag.SavedEventIds = savedEventIds.ToHashSet();

        return View(events);
    }

    public async Task<IActionResult> EventDetails(int? id)
    {
        ViewData["Title"] = "Event Details";

        EventItem? eventItem;
        if (id.HasValue)
        {
            eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == id.Value);
        }
        else
        {
            eventItem = await db.Events.FirstOrDefaultAsync(e => e.Title == "Tech Summit 2026")
                ?? await db.Events.FirstOrDefaultAsync();
        }

        if (eventItem is null)
        {
            return RedirectToAction(nameof(BrowseEvents));
        }

        var model = new EventDetailsViewModel
        {
            Event = eventItem,
            TicketOptions = await db.TicketOptions.Where(t => t.EventItemId == eventItem.Id).ToListAsync(),
            Speakers = await db.Speakers.Where(s => s.EventItemId == eventItem.Id).ToListAsync(),
            Reviews = await db.Reviews.Where(r => r.EventItemId == eventItem.Id).ToListAsync()
        };

        return View(model);
    }
    public async Task<IActionResult> Payment(int? id, string? ticket, decimal? price)
    {
        ViewData["Title"] = "Payment";
        ViewData["AttendNav"] = "";

        EventItem? eventItem;
        if (id.HasValue)
        {
            eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == id.Value);
        }
        else
        {
            eventItem = await db.Events.FirstOrDefaultAsync(e => e.Title == "Tech Summit 2026")
                ?? await db.Events.FirstOrDefaultAsync();
        }

        if (eventItem is null)
        {
            return RedirectToAction(nameof(BrowseEvents));
        }

        var model = new PaymentViewModel
        {
            Event = eventItem,
            TicketName = string.IsNullOrWhiteSpace(ticket) ? "Regular" : ticket,
            TicketPrice = price ?? (eventItem.Price > 0 ? eventItem.Price : 99),
            Discount = 0
        };

        return View(model);
    }

    public async Task<IActionResult> PaymentSuccess(int? id, string? ticket, decimal? total, int qty = 1)
    {
        ViewData["Title"] = "Payment Success";
        ViewData["AttendNav"] = "";

        EventItem? eventItem;
        if (id.HasValue)
        {
            eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == id.Value);
        }
        else
        {
            eventItem = await db.Events.FirstOrDefaultAsync(e => e.Title == "Tech Summit 2026")
                ?? await db.Events.FirstOrDefaultAsync();
        }

        if (eventItem is null)
        {
            return RedirectToAction(nameof(BrowseEvents));
        }

        var totalPaid = total ?? (eventItem.Price > 0 ? eventItem.Price : 99);
        var model = new PaymentSuccessViewModel
        {
            Event = eventItem,
            TicketName = string.IsNullOrWhiteSpace(ticket) ? "Regular" : ticket,
            Quantity = qty < 1 ? 1 : qty,
            TotalPaid = totalPaid,
            BookingId = $"EVT-{DateTime.UtcNow:yyyyMMdd}-{eventItem.Id:000}"
        };

        var hasMyBooking = await db.MyBookings.AnyAsync(b => b.BookingCode == model.BookingId);
        if (!hasMyBooking)
        {
            db.MyBookings.Add(new MyBooking
            {
                BookingCode = model.BookingId,
                EventItemId = eventItem.Id,
                UserEmail = "attend@eventify.com",
                TicketName = model.TicketName,
                Quantity = model.Quantity,
                UnitPrice = model.Quantity > 0 ? model.TotalPaid / model.Quantity : model.TotalPaid,
                TotalAmount = model.TotalPaid,
                Status = "Booked"
            });
            await db.SaveChangesAsync();
        }

        return View(model);
    }

    public async Task<IActionResult> MyBookings(string? tab)
    {
        ViewData["Title"] = "My Bookings";
        ViewData["AttendNav"] = "bookings";

        var activeTab = (tab ?? "all").Trim().ToLowerInvariant();
        if (activeTab != "all" && activeTab != "upcoming" && activeTab != "canceled")
        {
            activeTab = "all";
        }

        var query = db.MyBookings
            .Where(b => b.UserEmail == "attend@eventify.com")
            .Join(
                db.Events,
                b => b.EventItemId,
                e => e.Id,
                (b, e) => new { Booking = b, Event = e }
            );

        if (activeTab == "upcoming")
        {
            query = query.Where(x => x.Booking.Status == "Booked" && x.Event.StartDateTime >= DateTime.Today);
        }
        else if (activeTab == "canceled")
        {
            query = query.Where(x => x.Booking.Status == "Canceled");
        }

        var rows = await query
            .OrderByDescending(x => x.Booking.CreatedAtUtc)
            .ToListAsync();

        var model = new MyBookingsViewModel
        {
            ActiveTab = activeTab,
            Items = rows.Select(x => new MyBookingItemViewModel
            {
                Id = x.Booking.Id,
                EventId = x.Event.Id,
                BookingCode = $"BK{x.Booking.Id:000000}",
                EventTitle = x.Event.Title,
                ImageUrl = x.Event.ImageUrl,
                EventDate = x.Event.StartDateTime,
                TimeRange = $"{x.Event.StartDateTime:h:mm tt} - {x.Event.StartDateTime.AddHours(9):h:mm tt}",
                Location = x.Event.Location,
                TicketLabel = string.IsNullOrWhiteSpace(x.Booking.TicketName) ? "Regular" : x.Booking.TicketName,
                AccessLabel = x.Booking.TotalAmount > 0 ? "Regular Access" : "Standard Access",
                Price = x.Booking.UnitPrice,
                Quantity = x.Booking.Quantity <= 0 ? 1 : x.Booking.Quantity,
                IsCanceled = x.Booking.Status == "Canceled"
            }).ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> Wishlist()
    {
        ViewData["Title"] = "Wishlist";
        ViewData["AttendNav"] = "wishlist";

        var rows = await db.Bookings
            .Where(b => b.UserEmail == "attend@eventify.com" && b.IsSaved)
            .Join(
                db.Events,
                b => b.EventItemId,
                e => e.Id,
                (b, e) => new { Booking = b, Event = e }
            )
            .OrderByDescending(x => x.Booking.CreatedAtUtc)
            .ToListAsync();

        var items = rows
            .GroupBy(x => x.Event.Id)
            .Select(g => g.First())
            .Select(x => new WishlistItemViewModel
            {
                EventId = x.Event.Id,
                BookingId = x.Booking.Id,
                Title = x.Event.Title,
                Category = string.IsNullOrWhiteSpace(x.Event.Category) ? "Event" : x.Event.Category,
                EventDate = x.Event.StartDateTime,
                Location = x.Event.Location,
                Price = x.Event.Price,
                ImageUrl = x.Event.ImageUrl,
                Rating = x.Event.Rating,
                ReviewCount = x.Event.ReviewCount
            })
            .ToList();

        return View(new WishlistViewModel { Items = items });
    }

    public async Task<IActionResult> Notifications()
    {
        ViewData["Title"] = "Notifications";
        ViewData["AttendNav"] = "notifications";

        var rows = await db.AttendNotifications
            .Where(n => n.UserEmail == "attend@eventify.com")
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.CreatedAtUtc)
            .ToListAsync();

        var model = new AttendNotificationsViewModel
        {
            UnreadCount = rows.Count(n => !n.IsRead),
            Items = rows.Select(n => new AttendNotificationItemViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Kind = n.Kind,
                IsUnread = !n.IsRead,
                RelativeTime = ToRelativeTime(n.CreatedAtUtc)
            }).ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> ProfileSettings(string? tab, string? message, string? error)
    {
        var activeTab = (tab ?? "edit").Trim().ToLowerInvariant();
        if (activeTab != "edit" && activeTab != "password" && activeTab != "notifications")
        {
            activeTab = "edit";
        }

        var model = await GetProfileSettingsModelAsync(activeTab);
        model.Message = message ?? string.Empty;
        model.Error = error ?? string.Empty;

        ViewData["Title"] = "Profile Settings";
        ViewData["AttendNav"] = "profile";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(AttendProfileSettingsViewModel input)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "attend@eventify.com");
        if (user is null)
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", error = "User not found." });
        }

        var settings = await db.AttendProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == "attend@eventify.com");
        if (settings is null)
        {
            settings = new AttendProfileSetting { UserEmail = "attend@eventify.com" };
            db.AttendProfileSettings.Add(settings);
        }

        user.FullName = string.IsNullOrWhiteSpace(input.FullName) ? user.FullName : input.FullName.Trim();
        settings.PhoneNumber = input.PhoneNumber?.Trim() ?? string.Empty;
        settings.Location = input.Location?.Trim() ?? string.Empty;
        settings.DateOfBirth = input.DateOfBirth;
        settings.Bio = input.Bio?.Trim() ?? string.Empty;
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", message = "Profile updated." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotificationPreferences(AttendProfileSettingsViewModel input)
    {
        var settings = await db.AttendProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == "attend@eventify.com");
        if (settings is null)
        {
            settings = new AttendProfileSetting { UserEmail = "attend@eventify.com" };
            db.AttendProfileSettings.Add(settings);
        }

        settings.EmailNotifications = input.EmailNotifications;
        settings.PushNotifications = input.PushNotifications;
        settings.EventReminders = input.EventReminders;
        settings.PromotionsOffers = input.PromotionsOffers;
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(ProfileSettings), new { tab = "notifications", message = "Preferences saved." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(AttendProfileSettingsViewModel input)
    {
        if (string.IsNullOrWhiteSpace(input.CurrentPassword) || string.IsNullOrWhiteSpace(input.NewPassword))
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "password", error = "All password fields are required." });
        }

        if (input.NewPassword != input.ConfirmPassword)
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "password", error = "Confirm password does not match." });
        }

        if (input.NewPassword.Length < 8)
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "password", error = "New password must be at least 8 characters." });
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "attend@eventify.com");
        if (user is null)
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "password", error = "User not found." });
        }

        var currentHash = PasswordHasher.Hash(input.CurrentPassword);
        if (!string.Equals(user.PasswordHash, currentHash, StringComparison.Ordinal))
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "password", error = "Current password is incorrect." });
        }

        user.PasswordHash = PasswordHasher.Hash(input.NewPassword);
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(ProfileSettings), new { tab = "password", message = "Password updated." });
    }

    public async Task<IActionResult> Reviews()
    {
        ViewData["Title"] = "Reviews";
        ViewData["AttendNav"] = "reviews";

        var reviewedRows = await db.AttendReviews
            .Where(r => r.UserEmail == "attend@eventify.com")
            .Join(
                db.Events,
                r => r.EventItemId,
                e => e.Id,
                (r, e) => new { Review = r, Event = e }
            )
            .OrderByDescending(x => x.Review.ReviewedAtUtc)
            .ToListAsync();

        var reviewedEventIds = reviewedRows.Select(x => x.Event.Id).Distinct().ToHashSet();

        var pendingRows = await db.MyBookings
            .Where(b => b.UserEmail == "attend@eventify.com" && b.Status == "Booked")
            .Join(
                db.Events,
                b => b.EventItemId,
                e => e.Id,
                (b, e) => new { Booking = b, Event = e }
            )
            .OrderByDescending(x => x.Booking.CreatedAtUtc)
            .ToListAsync();

        var model = new AttendReviewsViewModel
        {
            Reviews = reviewedRows.Select(x => new AttendReviewItemViewModel
            {
                ReviewId = x.Review.Id,
                EventId = x.Event.Id,
                EventTitle = x.Event.Title,
                ImageUrl = x.Event.ImageUrl,
                Rating = x.Review.Rating,
                Comment = x.Review.Comment,
                ReviewedAt = x.Review.ReviewedAtUtc.ToLocalTime()
            }).ToList(),
            Pending = pendingRows
                .Where(x => !reviewedEventIds.Contains(x.Event.Id))
                .GroupBy(x => x.Event.Id)
                .Select(g => g.First())
                .Take(5)
                .Select(x => new AttendPendingReviewItemViewModel
                {
                    EventId = x.Event.Id,
                    EventTitle = x.Event.Title,
                    ImageUrl = x.Event.ImageUrl,
                    AttendedOn = x.Booking.CreatedAtUtc.ToLocalTime()
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttendReview(int id)
    {
        var review = await db.AttendReviews.FirstOrDefaultAsync(r => r.Id == id && r.UserEmail == "attend@eventify.com");
        if (review is not null)
        {
            db.AttendReviews.Remove(review);
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Reviews));
    }

    public async Task<IActionResult> WriteReview(int? eventId, int? reviewId)
    {
        ViewData["Title"] = "Write Review";
        ViewData["AttendNav"] = "reviews";

        if (reviewId.HasValue)
        {
            var row = await db.AttendReviews
                .Where(r => r.Id == reviewId.Value && r.UserEmail == "attend@eventify.com")
                .Join(
                    db.Events,
                    r => r.EventItemId,
                    e => e.Id,
                    (r, e) => new { Review = r, Event = e }
                )
                .FirstOrDefaultAsync();

            if (row is null)
            {
                return RedirectToAction(nameof(Reviews));
            }

            return View(new WriteAttendReviewViewModel
            {
                ReviewId = row.Review.Id,
                EventId = row.Event.Id,
                EventTitle = row.Event.Title,
                Rating = row.Review.Rating,
                Comment = row.Review.Comment
            });
        }

        if (!eventId.HasValue)
        {
            return RedirectToAction(nameof(Reviews));
        }

        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId.Value);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(Reviews));
        }

        return View(new WriteAttendReviewViewModel
        {
            EventId = eventItem.Id,
            EventTitle = eventItem.Title,
            Rating = 5
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReview(WriteAttendReviewViewModel model)
    {
        ViewData["Title"] = "Write Review";
        ViewData["AttendNav"] = "reviews";

        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == model.EventId);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(Reviews));
        }

        model.EventTitle = eventItem.Title;
        if (!ModelState.IsValid)
        {
            return View("WriteReview", model);
        }

        if (model.ReviewId.HasValue)
        {
            var review = await db.AttendReviews.FirstOrDefaultAsync(r => r.Id == model.ReviewId.Value && r.UserEmail == "attend@eventify.com");
            if (review is not null)
            {
                review.Rating = model.Rating;
                review.Comment = model.Comment.Trim();
                review.ReviewedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        else
        {
            var existing = await db.AttendReviews.FirstOrDefaultAsync(r => r.UserEmail == "attend@eventify.com" && r.EventItemId == model.EventId);
            if (existing is null)
            {
                db.AttendReviews.Add(new AttendReview
                {
                    EventItemId = model.EventId,
                    UserEmail = "attend@eventify.com",
                    Rating = model.Rating,
                    Comment = model.Comment.Trim(),
                    ReviewedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.Rating = model.Rating;
                existing.Comment = model.Comment.Trim();
                existing.ReviewedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPendingReview(int eventId)
    {
        var exists = await db.AttendReviews.AnyAsync(r => r.UserEmail == "attend@eventify.com" && r.EventItemId == eventId);
        if (!exists)
        {
            db.AttendReviews.Add(new AttendReview
            {
                EventItemId = eventId,
                UserEmail = "attend@eventify.com",
                Rating = 5,
                Comment = "Great event experience. Would definitely recommend."
            });
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        var rows = await db.AttendNotifications
            .Where(n => n.UserEmail == "attend@eventify.com" && !n.IsRead)
            .ToListAsync();

        if (rows.Count > 0)
        {
            foreach (var row in rows)
            {
                row.IsRead = true;
            }
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Notifications));
    }

    public async Task<IActionResult> BookingInvoice(int id)
    {
        ViewData["Title"] = "Invoice";
        ViewData["AttendNav"] = "bookings";

        var row = await db.MyBookings
            .Where(b => b.Id == id && b.UserEmail == "attend@eventify.com")
            .Join(
                db.Events,
                b => b.EventItemId,
                e => e.Id,
                (b, e) => new { Booking = b, Event = e }
            )
            .FirstOrDefaultAsync();

        if (row is null)
        {
            return RedirectToAction(nameof(MyBookings));
        }

        var subtotal = row.Booking.TotalAmount > 0 ? row.Booking.TotalAmount : row.Booking.UnitPrice * Math.Max(1, row.Booking.Quantity);
        var discount = 0m;
        var total = subtotal - discount;

        var model = new BookingInvoiceViewModel
        {
            BookingId = row.Booking.Id,
            BookingCode = row.Booking.BookingCode,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyMMdd}-{row.Booking.Id:0000}",
            IssuedOn = DateTime.Now,
            EventId = row.Event.Id,
            EventTitle = row.Event.Title,
            EventDate = row.Event.StartDateTime,
            EventTime = $"{row.Event.StartDateTime:h:mm tt} - {row.Event.StartDateTime.AddHours(9):h:mm tt}",
            EventLocation = row.Event.Location,
            UserName = "attend",
            UserEmail = row.Booking.UserEmail,
            TicketName = string.IsNullOrWhiteSpace(row.Booking.TicketName) ? "Regular" : row.Booking.TicketName,
            Quantity = Math.Max(1, row.Booking.Quantity),
            UnitPrice = row.Booking.UnitPrice,
            Subtotal = subtotal,
            Discount = discount,
            Total = total
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromWishlist(int eventId)
    {
        var savedRows = await db.Bookings
            .Where(b => b.UserEmail == "attend@eventify.com" && b.EventItemId == eventId && b.IsSaved)
            .ToListAsync();

        if (savedRows.Count > 0)
        {
            foreach (var row in savedRows)
            {
                row.IsSaved = false;
                if (row.Status == "Saved")
                {
                    row.Status = "Booked";
                }
            }
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Wishlist));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookFromWishlist(int eventId)
    {
        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(Wishlist));
        }

        db.Bookings.Add(new Booking
        {
            EventItemId = eventId,
            UserEmail = "attend@eventify.com",
            Status = "Booked",
            IsSaved = false
        });
        await db.SaveChangesAsync();

        var bookingCode = $"BK{DateTime.UtcNow:yyyyMMddHHmmss}{eventId:000}";
        db.MyBookings.Add(new MyBooking
        {
            BookingCode = bookingCode,
            EventItemId = eventId,
            UserEmail = "attend@eventify.com",
            TicketName = "Regular",
            Quantity = 1,
            UnitPrice = eventItem.Price,
            TotalAmount = eventItem.Price,
            Status = "Booked"
        });

        var savedRows = await db.Bookings
            .Where(b => b.UserEmail == "attend@eventify.com" && b.EventItemId == eventId && b.IsSaved)
            .ToListAsync();
        foreach (var row in savedRows)
        {
            row.IsSaved = false;
            if (row.Status == "Saved")
            {
                row.Status = "Booked";
            }
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(MyBookings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id, string? tab)
    {
        var booking = await db.MyBookings.FirstOrDefaultAsync(b => b.Id == id && b.UserEmail == "attend@eventify.com");
        if (booking is not null && booking.Status != "Canceled")
        {
            booking.Status = "Canceled";
            var linkedBooking = await db.Bookings
                .Where(b => b.EventItemId == booking.EventItemId && b.UserEmail == booking.UserEmail && b.Status != "Canceled")
                .OrderByDescending(b => b.CreatedAtUtc)
                .FirstOrDefaultAsync();
            if (linkedBooking is not null)
            {
                linkedBooking.Status = "Canceled";
            }
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(MyBookings), new { tab = string.IsNullOrWhiteSpace(tab) ? "all" : tab });
    }

    public async Task<IActionResult> DownloadTicket(int id, string? ticket, decimal? total, int qty = 1, string? bookingId = null)
    {
        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(BrowseEvents));
        }

        var resolvedTicket = string.IsNullOrWhiteSpace(ticket) ? "Regular" : ticket;
        var resolvedQty = qty < 1 ? 1 : qty;
        var resolvedTotal = total ?? (eventItem.Price > 0 ? eventItem.Price : 99);
        var resolvedBookingId = string.IsNullOrWhiteSpace(bookingId)
            ? $"EVT-{DateTime.UtcNow:yyyyMMdd}-{eventItem.Id:000}"
            : bookingId;

        var ticketText = $"""
        Eventify - Ticket Confirmation
        ------------------------------
        Booking ID: {resolvedBookingId}
        Event: {eventItem.Title}
        Date: {eventItem.StartDateTime:MMMM d, yyyy}
        Time: {eventItem.StartDateTime:h:mm tt}
        Location: {eventItem.Location}
        Ticket: {resolvedTicket}
        Quantity: {resolvedQty}
        Total Paid: ₹{resolvedTotal:0.00}
        ------------------------------
        Please carry this ticket and a valid ID proof at the venue.
        """;

        var bytes = Encoding.UTF8.GetBytes(ticketText);
        var fileName = $"Ticket-{resolvedBookingId}.txt";
        return File(bytes, "text/plain", fileName);
    }

    public async Task<IActionResult> DownloadInvoicePdf(int id, string? ticket, decimal? total, int qty = 1, string? bookingId = null)
    {
        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(BrowseEvents));
        }

        var myBooking = string.IsNullOrWhiteSpace(bookingId)
            ? null
            : await db.MyBookings.FirstOrDefaultAsync(b => b.BookingCode == bookingId && b.EventItemId == id);

        var resolvedTicket = myBooking?.TicketName ?? (string.IsNullOrWhiteSpace(ticket) ? "Regular" : ticket);
        var resolvedQty = myBooking?.Quantity ?? (qty < 1 ? 1 : qty);
        var unitPrice = myBooking?.UnitPrice ?? total ?? (eventItem.Price > 0 ? eventItem.Price : 99);
        var subtotal = unitPrice * resolvedQty;
        var discount = Math.Round(subtotal * 0.10m, 2);
        var finalTotal = subtotal - discount;
        var resolvedBookingId = string.IsNullOrWhiteSpace(bookingId)
            ? $"EVT-{DateTime.UtcNow:yyyyMMdd}-{eventItem.Id:000}"
            : bookingId;
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{eventItem.Id:000}";
        var customerEmail = await db.Bookings
            .Where(b => b.EventItemId == eventItem.Id && b.Status == "Booked")
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => b.UserEmail)
            .FirstOrDefaultAsync() ?? "attend@eventify.com";

        var pdfBytes = BuildInvoicePdf(
            invoiceNumber,
            resolvedBookingId,
            eventItem,
            customerEmail,
            resolvedTicket,
            resolvedQty,
            unitPrice,
            subtotal,
            discount,
            finalTotal
        );

        return File(pdfBytes, "application/pdf", $"Invoice-{invoiceNumber}.pdf");
    }

    private static byte[] BuildInvoicePdf(
        string invoiceNumber,
        string bookingId,
        EventItem eventItem,
        string customerEmail,
        string ticketName,
        int qty,
        decimal unitPrice,
        decimal subtotal,
        decimal discount,
        decimal total)
    {
        var issueDate = DateTime.Now.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var eventDate = eventItem.StartDateTime.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        var eventTime = eventItem.StartDateTime.ToString("h:mm tt", CultureInfo.InvariantCulture);
        var lineDescription = $"{ticketName} - {eventItem.Title}";

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("q");
        contentBuilder.AppendLine("0.18 0.34 0.86 rg");
        contentBuilder.AppendLine("20 760 290 60 re f");
        contentBuilder.AppendLine("0.06 0.72 0.82 rg");
        contentBuilder.AppendLine("310 760 265 60 re f");
        contentBuilder.AppendLine("Q");

        contentBuilder.AppendLine("0.93 0.95 0.98 rg");
        contentBuilder.AppendLine("20 675 265 78 re f");
        contentBuilder.AppendLine("300 675 275 78 re f");
        contentBuilder.AppendLine("0.95 0.96 0.98 rg");
        contentBuilder.AppendLine("20 595 555 70 re f");
        contentBuilder.AppendLine("20 510 555 76 re f");

        contentBuilder.AppendLine("0.72 0.79 0.90 RG");
        contentBuilder.AppendLine("1 w");
        contentBuilder.AppendLine("20 675 265 78 re S");
        contentBuilder.AppendLine("300 675 275 78 re S");
        contentBuilder.AppendLine("20 595 555 70 re S");
        contentBuilder.AppendLine("20 510 555 76 re S");

        AddPdfText(contentBuilder, "F2", 24, 1, 1, 1, 34, 790, "Eventify Invoice");
        AddPdfText(contentBuilder, "F2", 10, 1, 1, 1, 455, 806, $"Invoice: {invoiceNumber}");
        AddPdfText(contentBuilder, "F1", 9, 1, 1, 1, 462, 790, $"Issued: {issueDate}");

        AddPdfText(contentBuilder, "F2", 11, 0.08, 0.15, 0.31, 30, 736, "Customer");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 720, "attend");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 706, customerEmail);
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 692, "+91 90000 00000");

        AddPdfText(contentBuilder, "F2", 11, 0.08, 0.15, 0.31, 310, 736, "Event");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 310, 720, eventItem.Title);
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 310, 706, $"{eventDate} | {eventTime}");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 310, 692, eventItem.Location);

        AddPdfText(contentBuilder, "F2", 10, 0.08, 0.15, 0.31, 30, 647, "Description");
        AddPdfText(contentBuilder, "F2", 10, 0.08, 0.15, 0.31, 360, 647, "Qty");
        AddPdfText(contentBuilder, "F2", 10, 0.08, 0.15, 0.31, 420, 647, "Unit Price");
        AddPdfText(contentBuilder, "F2", 10, 0.08, 0.15, 0.31, 515, 647, "Line Total");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 625, lineDescription);
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 365, 625, qty.ToString(CultureInfo.InvariantCulture));
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 420, 625, $"INR {unitPrice:0.00}");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 515, 625, $"INR {subtotal:0.00}");

        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 425, 567, "Subtotal");
        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 515, 567, $"INR {subtotal:0.00}");
        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 425, 551, "Discount (10%)");
        AddPdfText(contentBuilder, "F1", 10, 0.00, 0.55, 0.35, 510, 551, $"-INR {discount:0.00}");
        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 425, 535, "Tax");
        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 525, 535, "INR 0.00");
        AddPdfText(contentBuilder, "F2", 16, 0.18, 0.34, 0.86, 425, 515, "Total");
        AddPdfText(contentBuilder, "F2", 16, 0.18, 0.34, 0.86, 490, 515, $"INR {total:0.00}");

        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 496, $"Booking ID: {bookingId}");
        AddPdfText(contentBuilder, "F1", 8, 0.20, 0.30, 0.45, 20, 24, "Thank you for booking with Eventify. This is a system-generated invoice.");

        var contentStream = contentBuilder.ToString();
        var contentLength = Encoding.ASCII.GetByteCount(contentStream);

        var objects = new List<string>
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>\nendobj\n",
            $"4 0 obj\n<< /Length {contentLength} >>\nstream\n{contentStream}endstream\nendobj\n",
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            "6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n"
        };

        var pdfBuilder = new StringBuilder();
        pdfBuilder.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdfBuilder.ToString()));
            pdfBuilder.Append(obj);
        }

        var xrefStart = Encoding.ASCII.GetByteCount(pdfBuilder.ToString());
        pdfBuilder.Append($"xref\n0 {objects.Count + 1}\n");
        pdfBuilder.Append("0000000000 65535 f \n");
        for (var i = 1; i <= objects.Count; i++)
        {
            pdfBuilder.Append($"{offsets[i]:D10} 00000 n \n");
        }

        pdfBuilder.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        pdfBuilder.Append($"startxref\n{xrefStart}\n%%EOF");

        return Encoding.ASCII.GetBytes(pdfBuilder.ToString());
    }

    private static void AddPdfText(
        StringBuilder builder,
        string fontName,
        int fontSize,
        double red,
        double green,
        double blue,
        int x,
        int y,
        string text)
    {
        builder.AppendLine("BT");
        builder.AppendLine($"/{fontName} {fontSize} Tf");
        builder.AppendLine($"{red:0.###} {green:0.###} {blue:0.###} rg");
        builder.AppendLine($"{x} {y} Td");
        builder.AppendLine($"({EscapePdfText(text)}) Tj");
        builder.AppendLine("ET");
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static string ToRelativeTime(DateTime utcDate)
    {
        var span = DateTime.UtcNow - utcDate;
        if (span.TotalHours < 1)
        {
            var mins = Math.Max(1, (int)Math.Round(span.TotalMinutes));
            return $"{mins} min ago";
        }

        if (span.TotalHours < 24)
        {
            var hours = (int)Math.Round(span.TotalHours);
            return $"{hours} hour{(hours == 1 ? "" : "s")} ago";
        }

        var days = (int)Math.Round(span.TotalDays);
        return $"{days} day{(days == 1 ? "" : "s")} ago";
    }

    private async Task<AttendProfileSettingsViewModel> GetProfileSettingsModelAsync(string activeTab)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "attend@eventify.com");
        if (user is null)
        {
            user = new UserAccount
            {
                FullName = "Attend User",
                Email = "attend@eventify.com",
                PasswordHash = PasswordHasher.Hash("Attend@2026!Go"),
                Role = "attend"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var settings = await db.AttendProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == user.Email);
        if (settings is null)
        {
            settings = new AttendProfileSetting
            {
                UserEmail = user.Email,
                PhoneNumber = "+91 90000 00000",
                Location = "Rajkot, Gujarat"
            };
            db.AttendProfileSettings.Add(settings);
            await db.SaveChangesAsync();
        }

        return new AttendProfileSettingsViewModel
        {
            ActiveTab = activeTab,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = settings.PhoneNumber,
            Location = settings.Location,
            DateOfBirth = settings.DateOfBirth,
            Bio = settings.Bio,
            EmailNotifications = settings.EmailNotifications,
            PushNotifications = settings.PushNotifications,
            EventReminders = settings.EventReminders,
            PromotionsOffers = settings.PromotionsOffers
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSaved(int eventId)
    {
        var existing = await db.Bookings
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(b => b.EventItemId == eventId && b.UserEmail == "attend@eventify.com");

        if (existing is null)
        {
            db.Bookings.Add(new Booking
            {
                EventItemId = eventId,
                UserEmail = "attend@eventify.com",
                Status = "Saved",
                IsSaved = true
            });
        }
        else
        {
            existing.IsSaved = !existing.IsSaved;
            if (existing.IsSaved && existing.Status == "Canceled")
            {
                existing.Status = "Saved";
            }
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(BrowseEvents));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int eventId)
    {
        var eventExists = await db.Events.AnyAsync(e => e.Id == eventId);
        if (eventExists)
        {
            var booking = new Booking
            {
                EventItemId = eventId,
                UserEmail = "attend@eventify.com",
                Status = "Booked",
                IsSaved = false
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            var eventItem = await db.Events.FirstAsync(e => e.Id == eventId);
            db.MyBookings.Add(new MyBooking
            {
                BookingCode = $"BK{booking.Id:000000}",
                EventItemId = eventId,
                UserEmail = "attend@eventify.com",
                TicketName = "Regular",
                Quantity = 1,
                UnitPrice = eventItem.Price,
                TotalAmount = eventItem.Price,
                Status = "Booked"
            });
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Dashboard));
    }
}

