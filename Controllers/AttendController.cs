using Eventify.Data;
using Eventify.Models;
using Eventify.ViewModels;
using Eventify.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Eventify.Controllers;

public class AttendController(EventifyDbContext db, IWebHostEnvironment env, IConfiguration config) : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Dashboard()
    {
        ViewData["Title"] = "Dashboard";
        ViewData["AttendNav"] = "dashboard";

        var currentUserEmail = ResolveAttendEmail();

        var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Email == currentUserEmail);
        var displayName = currentUser?.FullName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = HttpContext.Session.GetString("UserFullName");
        }
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Attend User";
        }

        var bookedEventIds = await db.MyBookings
            .Where(b => b.UserEmail == currentUserEmail && b.Status == "Booked")
            .Select(b => b.EventItemId)
            .Distinct()
            .ToListAsync();

        var upcomingEvents = await db.Events
            .Where(e => bookedEventIds.Contains(e.Id))
            .Where(e => e.StartDateTime >= DateTime.Today)
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
            .Where(e => !bookedEventIds.Contains(e.Id))
            .OrderByDescending(e => e.Rating)
            .Take(3)
            .ToListAsync();

        if (recommendedEvents.Count == 0)
        {
            recommendedEvents = await db.Events
                .OrderByDescending(e => e.Rating)
                .Take(3)
                .ToListAsync();
        }

        var model = new AttendDashboardViewModel
        {
            UserDisplayName = displayName,
            TotalBookings = await db.MyBookings.CountAsync(b => b.UserEmail == currentUserEmail && b.Status == "Booked"),
            UpcomingCount = await db.MyBookings
                .Where(b => b.UserEmail == currentUserEmail && b.Status == "Booked")
                .Join(db.Events, b => b.EventItemId, e => e.Id, (b, e) => e)
                .CountAsync(e => e.StartDateTime >= DateTime.Today),
            CanceledCount = await db.MyBookings.CountAsync(b => b.UserEmail == currentUserEmail && b.Status == "Canceled"),
            SavedCount = await db.Bookings.CountAsync(b => b.UserEmail == currentUserEmail && b.IsSaved),
            UpcomingEvents = upcomingEvents,
            RecommendedEvents = recommendedEvents
        };

        return View(model);
    }

    public async Task<IActionResult> BrowseEvents()
    {
        ViewData["Title"] = "Browse Events";
        ViewData["AttendNav"] = "browse";
        var currentUserEmail = ResolveAttendEmail();

        var events = await db.Events
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();

        var savedEventIds = await db.Bookings
            .Where(b => b.UserEmail == currentUserEmail && b.IsSaved)
            .Select(b => b.EventItemId)
            .Distinct()
            .ToListAsync();

        var soldRows = await db.MyBookings
            .Where(b => b.Status == "Booked")
            .GroupBy(b => b.EventItemId)
            .Select(g => new
            {
                EventItemId = g.Key,
                Sold = g.Sum(x => x.Quantity <= 0 ? 1 : x.Quantity)
            })
            .ToListAsync();

        var soldByEvent = soldRows.ToDictionary(x => x.EventItemId, x => x.Sold);
        var capacityByEvent = await db.OrganizerEventConfigs
            .ToDictionaryAsync(x => x.EventItemId, x => x.AvailableQuantity);

        ViewBag.SavedEventIds = savedEventIds.ToHashSet();
        ViewBag.SoldByEvent = soldByEvent;
        ViewBag.CapacityByEvent = capacityByEvent;

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
    public async Task<IActionResult> Payment(int? id, string? ticket, decimal? price, int qty = 1)
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

        var resolvedPrice = price ?? eventItem.Price;
        if (resolvedPrice <= 0)
        {
            return RedirectToAction(nameof(BookFree), new
            {
                eventId = eventItem.Id,
                ticket = string.IsNullOrWhiteSpace(ticket) ? "Free Pass" : ticket,
                qty = 1
            });
        }

        var model = new PaymentViewModel
        {
            Event = eventItem,
            TicketName = string.IsNullOrWhiteSpace(ticket) ? "Regular" : ticket,
            TicketPrice = resolvedPrice,
            Quantity = qty < 1 ? 1 : qty,
            Discount = 0
        };

        return View(model);
    }

    public async Task<IActionResult> BookFree(int eventId, string? ticket, int qty = 1)
    {
        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(BrowseEvents));
        }

        var currentUserEmail = ResolveAttendEmail();
        var resolvedQty = qty < 1 ? 1 : qty;
        var resolvedTicket = string.IsNullOrWhiteSpace(ticket) ? "Free Pass" : ticket.Trim();
        var bookingCode = $"FR-{DateTime.UtcNow:yyyyMMddHHmmss}-{eventItem.Id:000}";

        db.Bookings.Add(new Booking
        {
            EventItemId = eventItem.Id,
            UserEmail = currentUserEmail,
            Status = "Booked",
            IsSaved = false
        });

        db.MyBookings.Add(new MyBooking
        {
            BookingCode = bookingCode,
            EventItemId = eventItem.Id,
            UserEmail = currentUserEmail,
            TicketName = resolvedTicket,
            Quantity = resolvedQty,
            UnitPrice = 0,
            TotalAmount = 0,
            Status = "Booked"
        });

        await db.SaveChangesAsync();

        return RedirectToAction(nameof(PaymentSuccess), new
        {
            id = eventItem.Id,
            ticket = resolvedTicket,
            total = 0,
            qty = resolvedQty,
            bookingId = bookingCode
        });
    }

    public async Task<IActionResult> PaymentSuccess(int? id, string? ticket, decimal? total, int qty = 1, string? bookingId = null, string? method = null)
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

        var currentUserEmail = ResolveAttendEmail();
        var totalPaid = total ?? (eventItem.Price > 0 ? eventItem.Price : 0);
        var resolvedMethod = string.IsNullOrWhiteSpace(method)
            ? (totalPaid <= 0 ? "Free" : "UPI")
            : method.Trim();
        var paymentStatus = totalPaid <= 0 ? "Free" : "Success";
        var resolvedBookingId = string.IsNullOrWhiteSpace(bookingId)
            ? $"EVT-{DateTime.UtcNow:yyyyMMddHHmmss}-{eventItem.Id:000}"
            : bookingId;
        var model = new PaymentSuccessViewModel
        {
            Event = eventItem,
            TicketName = string.IsNullOrWhiteSpace(ticket) ? "Regular" : ticket,
            Quantity = qty < 1 ? 1 : qty,
            TotalPaid = totalPaid,
            BookingId = resolvedBookingId,
            PaymentMethod = resolvedMethod
        };

        var myBooking = await db.MyBookings
            .FirstOrDefaultAsync(b => b.BookingCode == model.BookingId && b.UserEmail == currentUserEmail);
        if (myBooking is null)
        {
            db.Bookings.Add(new Booking
            {
                EventItemId = eventItem.Id,
                UserEmail = currentUserEmail,
                Status = "Booked",
                IsSaved = false
            });

            myBooking = new MyBooking
            {
                BookingCode = model.BookingId,
                EventItemId = eventItem.Id,
                UserEmail = currentUserEmail,
                TicketName = model.TicketName,
                Quantity = model.Quantity,
                UnitPrice = model.Quantity > 0 ? model.TotalPaid / model.Quantity : model.TotalPaid,
                TotalAmount = model.TotalPaid,
                Status = "Booked"
            };
            db.MyBookings.Add(myBooking);
            await db.SaveChangesAsync();
        }

        var hasPaymentRecord = await db.OrganizerPaymentRecords
            .AnyAsync(p => p.MyBookingId == myBooking.Id);
        if (!hasPaymentRecord)
        {
            db.OrganizerPaymentRecords.Add(new OrganizerPaymentRecord
            {
                MyBookingId = myBooking.Id,
                TransactionId = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{myBooking.Id:0000}",
                Method = resolvedMethod,
                Status = paymentStatus,
                PaidAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return View(model);
    }

    public async Task<IActionResult> Payments(int page = 1)
    {
        ViewData["Title"] = "Payments";
        ViewData["AttendNav"] = "payments";

        var currentUserEmail = ResolveAttendEmail();
        const int pageSize = 10;
        if (page < 1)
        {
            page = 1;
        }

        var baseQuery = db.OrganizerPaymentRecords
            .Join(
                db.MyBookings.Where(b => b.UserEmail == currentUserEmail),
                p => p.MyBookingId,
                b => b.Id,
                (p, b) => new { Payment = p, Booking = b }
            )
            .Join(
                db.Events,
                x => x.Booking.EventItemId,
                e => e.Id,
                (x, e) => new { x.Payment, x.Booking, Event = e }
            );

        var totalCount = await baseQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var rows = await baseQuery
            .OrderByDescending(x => x.Payment.PaidAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var allRows = await baseQuery.ToListAsync();
        var model = new AttendPaymentsViewModel
        {
            TotalPaid = allRows
                .Where(x => x.Payment.Status == "Success")
                .Sum(x => x.Booking.TotalAmount),
            SuccessfulTransactions = allRows.Count(x => x.Payment.Status == "Success"),
            FreeBookings = allRows.Count(x => x.Payment.Status == "Free"),
            Rows = rows.Select(x => new AttendPaymentRowViewModel
            {
                TransactionId = x.Payment.TransactionId,
                Date = x.Payment.PaidAtUtc.ToLocalTime(),
                EventTitle = x.Event.Title,
                TicketName = string.IsNullOrWhiteSpace(x.Booking.TicketName) ? "Regular" : x.Booking.TicketName,
                Quantity = x.Booking.Quantity <= 0 ? 1 : x.Booking.Quantity,
                Amount = x.Booking.TotalAmount,
                Method = x.Payment.Method,
                Status = x.Payment.Status,
                BookingCode = string.IsNullOrWhiteSpace(x.Booking.BookingCode) ? $"BK{x.Booking.Id:000000}" : x.Booking.BookingCode
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return View(model);
    }

    public async Task<IActionResult> MyBookings(string? tab)
    {
        ViewData["Title"] = "My Bookings";
        ViewData["AttendNav"] = "bookings";
        var currentUserEmail = ResolveAttendEmail();

        var activeTab = (tab ?? "all").Trim().ToLowerInvariant();
        if (activeTab != "all" && activeTab != "upcoming" && activeTab != "canceled")
        {
            activeTab = "all";
        }

        var query = db.MyBookings
            .Where(b => b.UserEmail == currentUserEmail)
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
                BookingCode = string.IsNullOrWhiteSpace(x.Booking.BookingCode) ? $"BK{x.Booking.Id:000000}" : x.Booking.BookingCode,
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
        var currentUserEmail = ResolveAttendEmail();

        var rows = await db.Bookings
            .Where(b => b.UserEmail == currentUserEmail && b.IsSaved)
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
        var currentUserEmail = ResolveAttendEmail();

        var rows = await db.AttendNotifications
            .Where(n => n.UserEmail == currentUserEmail)
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

        var currentUserEmail = ResolveAttendEmail();
        var model = await GetProfileSettingsModelAsync(activeTab, currentUserEmail);
        model.Message = message ?? string.Empty;
        model.Error = error ?? string.Empty;

        ViewData["Title"] = "Profile Settings";
        ViewData["AttendNav"] = "profile";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(AttendProfileSettingsViewModel input, IFormFile? profilePhoto)
    {
        var currentUserEmail = ResolveAttendEmail();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == currentUserEmail);
        if (user is null)
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", error = "User not found." });
        }

        var settings = await db.AttendProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == currentUserEmail);
        if (settings is null)
        {
            settings = new AttendProfileSetting { UserEmail = currentUserEmail };
            db.AttendProfileSettings.Add(settings);
        }

        user.FullName = string.IsNullOrWhiteSpace(input.FullName) ? user.FullName : input.FullName.Trim();
        settings.PhoneNumber = input.PhoneNumber?.Trim() ?? string.Empty;
        settings.Location = input.Location?.Trim() ?? string.Empty;
        settings.DateOfBirth = input.DateOfBirth;
        settings.Bio = input.Bio?.Trim() ?? string.Empty;

        if (profilePhoto is not null && profilePhoto.Length > 0)
        {
            var extension = Path.GetExtension(profilePhoto.FileName);
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", error = "Only JPG, PNG, or WEBP files are allowed." });
            }

            var uploadsRoot = Path.Combine(env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var filePath = Path.Combine(uploadsRoot, fileName);
            await using (var stream = System.IO.File.Create(filePath))
            {
                await profilePhoto.CopyToAsync(stream);
            }

            if (!string.IsNullOrWhiteSpace(settings.ProfilePhotoPath) &&
                settings.ProfilePhotoPath.StartsWith("/uploads/profiles/", StringComparison.OrdinalIgnoreCase))
            {
                var oldFileName = Path.GetFileName(settings.ProfilePhotoPath);
                var oldPath = Path.Combine(uploadsRoot, oldFileName);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            settings.ProfilePhotoPath = $"/uploads/profiles/{fileName}";
        }

        await db.SaveChangesAsync();
        await RoleDatabaseMirror.MirrorUserAsync(config, user);
        HttpContext.Session.SetString("UserFullName", user.FullName);
        HttpContext.Session.SetString("UserPhotoPath", settings.ProfilePhotoPath ?? string.Empty);

        return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", message = "Profile updated." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile? profilePhoto)
    {
        if (profilePhoto is null || profilePhoto.Length <= 0)
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", error = "Please select a photo to upload." });
        }

        var extension = Path.GetExtension(profilePhoto.FileName);
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", error = "Only JPG, PNG, or WEBP files are allowed." });
        }

        var currentUserEmail = ResolveAttendEmail();
        var settings = await db.AttendProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == currentUserEmail);
        if (settings is null)
        {
            settings = new AttendProfileSetting { UserEmail = currentUserEmail };
            db.AttendProfileSettings.Add(settings);
        }

        var uploadsRoot = Path.Combine(env.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsRoot, fileName);
        await using (var stream = System.IO.File.Create(filePath))
        {
            await profilePhoto.CopyToAsync(stream);
        }

        if (!string.IsNullOrWhiteSpace(settings.ProfilePhotoPath) &&
            settings.ProfilePhotoPath.StartsWith("/uploads/profiles/", StringComparison.OrdinalIgnoreCase))
        {
            var oldFileName = Path.GetFileName(settings.ProfilePhotoPath);
            var oldPath = Path.Combine(uploadsRoot, oldFileName);
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }
        }

        settings.ProfilePhotoPath = $"/uploads/profiles/{fileName}";
        await db.SaveChangesAsync();
        HttpContext.Session.SetString("UserPhotoPath", settings.ProfilePhotoPath);

        return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", message = "Profile photo updated." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotificationPreferences(AttendProfileSettingsViewModel input)
    {
        var currentUserEmail = ResolveAttendEmail();
        var settings = await db.AttendProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == currentUserEmail);
        if (settings is null)
        {
            settings = new AttendProfileSetting { UserEmail = currentUserEmail };
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
        if (string.IsNullOrWhiteSpace(input.CurrentPassword) ||
            string.IsNullOrWhiteSpace(input.NewPassword) ||
            string.IsNullOrWhiteSpace(input.ConfirmPassword))
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

        var hasUpper = Regex.IsMatch(input.NewPassword, "[A-Z]");
        var hasLower = Regex.IsMatch(input.NewPassword, "[a-z]");
        var hasNumber = Regex.IsMatch(input.NewPassword, "[0-9]");
        var hasSpecial = Regex.IsMatch(input.NewPassword, "[^a-zA-Z0-9]");
        if (!hasUpper || !hasLower || !hasNumber || !hasSpecial)
        {
            return RedirectToAction(nameof(ProfileSettings), new
            {
                tab = "password",
                error = "Password must include uppercase, lowercase, number, and special character."
            });
        }

        var currentUserEmail = ResolveAttendEmail();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == currentUserEmail);
        if (user is null)
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "password", error = "User not found." });
        }

        var currentHash = PasswordHasher.Hash(input.CurrentPassword);
        if (!string.Equals(user.PasswordHash, currentHash, StringComparison.Ordinal))
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "password", error = "Current password is incorrect." });
        }

        var newHash = PasswordHasher.Hash(input.NewPassword);
        if (string.Equals(user.PasswordHash, newHash, StringComparison.Ordinal))
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "password", error = "New password must be different from current password." });
        }

        user.PasswordHash = newHash;
        await db.SaveChangesAsync();
        await RoleDatabaseMirror.MirrorUserAsync(config, user);

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
        var currentUserEmail = ResolveAttendEmail();
        var rows = await db.AttendNotifications
            .Where(n => n.UserEmail == currentUserEmail && !n.IsRead)
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

    public async Task<IActionResult> BookingInvoice(int? id, string? code)
    {
        ViewData["Title"] = "Invoice";
        ViewData["AttendNav"] = "bookings";
        var model = await BuildInvoiceModelAsync(id, code, null);
        if (model is null)
        {
            return RedirectToAction(nameof(MyBookings));
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromWishlist(int eventId)
    {
        var currentUserEmail = ResolveAttendEmail();
        var savedRows = await db.Bookings
            .Where(b => b.UserEmail == currentUserEmail && b.EventItemId == eventId && b.IsSaved)
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
        var currentUserEmail = ResolveAttendEmail();

        db.Bookings.Add(new Booking
        {
            EventItemId = eventId,
            UserEmail = currentUserEmail,
            Status = "Booked",
            IsSaved = false
        });
        await db.SaveChangesAsync();

        var bookingCode = $"BK{DateTime.UtcNow:yyyyMMddHHmmss}{eventId:000}";
        db.MyBookings.Add(new MyBooking
        {
            BookingCode = bookingCode,
            EventItemId = eventId,
            UserEmail = currentUserEmail,
            TicketName = "Regular",
            Quantity = 1,
            UnitPrice = eventItem.Price,
            TotalAmount = eventItem.Price,
            Status = "Booked"
        });

        var savedRows = await db.Bookings
            .Where(b => b.UserEmail == currentUserEmail && b.EventItemId == eventId && b.IsSaved)
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
        var currentUserEmail = ResolveAttendEmail();
        var booking = await db.MyBookings.FirstOrDefaultAsync(b => b.Id == id && b.UserEmail == currentUserEmail);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAllBookings()
    {
        var currentUserEmail = ResolveAttendEmail();

        var myBookingRows = await db.MyBookings
            .Where(b => b.UserEmail == currentUserEmail)
            .ToListAsync();

        if (myBookingRows.Count > 0)
        {
            db.MyBookings.RemoveRange(myBookingRows);
        }

        var linkedBookingRows = await db.Bookings
            .Where(b => b.UserEmail == currentUserEmail && (b.Status == "Booked" || b.Status == "Canceled" || !b.IsSaved))
            .ToListAsync();

        if (linkedBookingRows.Count > 0)
        {
            db.Bookings.RemoveRange(linkedBookingRows);
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(MyBookings), new { tab = "all" });
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
        Total Paid: ?{resolvedTotal:0.00}
        ------------------------------
        Please carry this ticket and a valid ID proof at the venue.
        """;

        var bytes = Encoding.UTF8.GetBytes(ticketText);
        var fileName = $"Ticket-{resolvedBookingId}.txt";
        return File(bytes, "text/plain", fileName);
    }

    public async Task<IActionResult> DownloadInvoicePdf(int id, string? ticket, decimal? total, int qty = 1, string? bookingId = null)
    {
        var model = await BuildInvoiceModelAsync(null, bookingId, id);
        if (model is null)
        {
            return RedirectToAction(nameof(MyBookings));
        }

        var pdfBytes = BuildInvoicePdf(model);
        return File(pdfBytes, "application/pdf", $"Invoice-{model.InvoiceNumber}.pdf");
    }

    private async Task<BookingInvoiceViewModel?> BuildInvoiceModelAsync(int? bookingRowId, string? bookingCode, int? eventId)
    {
        var currentUserEmail = ResolveAttendEmail();
        var query = db.MyBookings.Where(b => b.UserEmail == currentUserEmail);

        if (!string.IsNullOrWhiteSpace(bookingCode))
        {
            query = query.Where(b => b.BookingCode == bookingCode);
        }
        else if (bookingRowId.HasValue)
        {
            query = query.Where(b => b.Id == bookingRowId.Value);
        }
        else if (eventId.HasValue)
        {
            query = query.Where(b => b.EventItemId == eventId.Value).OrderByDescending(b => b.CreatedAtUtc);
        }
        else
        {
            return null;
        }

        var row = await query
            .Join(
                db.Events,
                b => b.EventItemId,
                e => e.Id,
                (b, e) => new { Booking = b, Event = e }
            )
            .FirstOrDefaultAsync();

        if (row is null)
        {
            return null;
        }

        var quantity = Math.Max(1, row.Booking.Quantity);
        var unitPrice = row.Booking.UnitPrice > 0 ? row.Booking.UnitPrice : row.Event.Price;
        var subtotal = row.Booking.TotalAmount > 0 ? row.Booking.TotalAmount : unitPrice * quantity;
        var discount = 0m;
        var total = subtotal - discount;

        var sessionName = HttpContext.Session.GetString("UserFullName");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == row.Booking.UserEmail);
        var profile = await db.AttendProfileSettings.FirstOrDefaultAsync(p => p.UserEmail == row.Booking.UserEmail);
        var issueDate = row.Booking.CreatedAtUtc == default ? DateTime.UtcNow : row.Booking.CreatedAtUtc;

        return new BookingInvoiceViewModel
        {
            BookingId = row.Booking.Id,
            BookingCode = string.IsNullOrWhiteSpace(row.Booking.BookingCode) ? $"BK{row.Booking.Id:000000}" : row.Booking.BookingCode,
            InvoiceNumber = $"INV-{issueDate:yyMMdd}-{row.Booking.Id:0000}",
            IssuedOn = issueDate.ToLocalTime(),
            EventId = row.Event.Id,
            EventTitle = row.Event.Title,
            EventDate = row.Event.StartDateTime,
            EventTime = $"{row.Event.StartDateTime:h:mm tt} - {row.Event.StartDateTime.AddHours(9):h:mm tt}",
            EventLocation = row.Event.Location,
            UserName = !string.IsNullOrWhiteSpace(sessionName)
                ? sessionName
                : string.IsNullOrWhiteSpace(user?.FullName) ? row.Booking.UserEmail : user!.FullName,
            UserEmail = row.Booking.UserEmail,
            UserPhone = string.IsNullOrWhiteSpace(profile?.PhoneNumber) ? "+91 90000 00000" : profile!.PhoneNumber,
            TicketName = string.IsNullOrWhiteSpace(row.Booking.TicketName) ? "Regular" : row.Booking.TicketName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Subtotal = subtotal,
            Discount = discount,
            Total = total
        };
    }

    private static byte[] BuildInvoicePdf(BookingInvoiceViewModel model)
    {
        var issueDate = model.IssuedOn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var eventDate = model.EventDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        var lineDescription = $"{model.TicketName} - {model.EventTitle}";

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

        AddPdfText(contentBuilder, "F2", 24, 1, 1, 1, 34, 790, "Tax Invoice");
        AddPdfText(contentBuilder, "F2", 10, 1, 1, 1, 455, 806, $"Invoice: {model.InvoiceNumber}");
        AddPdfText(contentBuilder, "F1", 9, 1, 1, 1, 462, 790, $"Issued: {issueDate}");

        AddPdfText(contentBuilder, "F2", 11, 0.08, 0.15, 0.31, 30, 736, "Bill To");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 720, model.UserName);
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 706, model.UserEmail);
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 692, model.UserPhone);

        AddPdfText(contentBuilder, "F2", 11, 0.08, 0.15, 0.31, 310, 736, "Event Details");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 310, 720, model.EventTitle);
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 310, 706, $"{eventDate} | {model.EventTime}");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 310, 692, model.EventLocation);

        AddPdfText(contentBuilder, "F2", 10, 0.08, 0.15, 0.31, 30, 647, "Description");
        AddPdfText(contentBuilder, "F2", 10, 0.08, 0.15, 0.31, 360, 647, "Qty");
        AddPdfText(contentBuilder, "F2", 10, 0.08, 0.15, 0.31, 420, 647, "Unit Price");
        AddPdfText(contentBuilder, "F2", 10, 0.08, 0.15, 0.31, 515, 647, "Line Total");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 625, lineDescription);
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 365, 625, model.Quantity.ToString(CultureInfo.InvariantCulture));
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 420, 625, $"INR {model.UnitPrice:0.00}");
        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 515, 625, $"INR {model.Subtotal:0.00}");

        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 425, 567, "Subtotal");
        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 515, 567, $"INR {model.Subtotal:0.00}");
        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 425, 551, "Discount");
        AddPdfText(contentBuilder, "F1", 10, 0.00, 0.55, 0.35, 510, 551, $"-INR {model.Discount:0.00}");
        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 425, 535, "Tax");
        AddPdfText(contentBuilder, "F1", 10, 0.08, 0.15, 0.31, 525, 535, "INR 0.00");
        AddPdfText(contentBuilder, "F2", 16, 0.18, 0.34, 0.86, 425, 515, "Total");
        AddPdfText(contentBuilder, "F2", 16, 0.18, 0.34, 0.86, 490, 515, $"INR {model.Total:0.00}");

        AddPdfText(contentBuilder, "F1", 9, 0.08, 0.15, 0.31, 30, 496, $"Booking ID: {model.BookingCode}");
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

    private string ResolveAttendEmail()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email.Trim().ToLowerInvariant();
        }

        return "attend@eventify.com";
    }

    private async Task<AttendProfileSettingsViewModel> GetProfileSettingsModelAsync(string activeTab, string currentUserEmail)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == currentUserEmail);
        if (user is null)
        {
            user = new UserAccount
            {
                FullName = HttpContext.Session.GetString("UserFullName") ?? "Attend User",
                Email = currentUserEmail,
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

        HttpContext.Session.SetString("UserFullName", user.FullName ?? "Attend User");
        HttpContext.Session.SetString("UserPhotoPath", settings.ProfilePhotoPath ?? string.Empty);

        return new AttendProfileSettingsViewModel
        {
            ActiveTab = activeTab,
            FullName = user.FullName ?? "Attend User",
            Email = user.Email,
            ProfilePhotoPath = settings.ProfilePhotoPath ?? string.Empty,
            PhoneNumber = settings.PhoneNumber ?? string.Empty,
            Location = settings.Location ?? string.Empty,
            DateOfBirth = settings.DateOfBirth,
            Bio = settings.Bio ?? string.Empty,
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
        var currentUserEmail = ResolveAttendEmail();
        var existing = await db.Bookings
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(b => b.EventItemId == eventId && b.UserEmail == currentUserEmail);

        if (existing is null)
        {
            db.Bookings.Add(new Booking
            {
                EventItemId = eventId,
                UserEmail = currentUserEmail,
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


