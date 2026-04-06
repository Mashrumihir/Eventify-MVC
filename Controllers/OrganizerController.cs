using Eventify.Data;
using Eventify.Models;
using Eventify.Utilities;
using Eventify.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Eventify.Controllers;

public class OrganizerController(EventifyDbContext db, IConfiguration config) : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Dashboard()
    {
        ViewData["Title"] = "Organizer Dashboard";
        ViewData["OrgNav"] = "dashboard";

        var bookedRows = await db.MyBookings
            .AsNoTracking()
            .Where(b => b.Status == "Booked")
            .Join(
                db.Events,
                b => b.EventItemId,
                e => e.Id,
                (b, e) => new { Booking = b, Event = e }
            )
            .OrderByDescending(x => x.Booking.CreatedAtUtc)
            .ToListAsync();

        var totalRevenue = bookedRows.Sum(x => x.Booking.TotalAmount);
        var ticketsSold = bookedRows.Sum(x => Math.Max(1, x.Booking.Quantity));
        var totalEvents = await db.Events.CountAsync();
        var bookingDates = await db.Bookings
            .AsNoTracking()
            .Select(b => b.CreatedAtUtc)
            .ToListAsync();
        var eventDates = await db.Events
            .AsNoTracking()
            .Select(e => e.StartDateTime)
            .ToListAsync();

        var today = DateTime.Today;
        var days = Enumerable.Range(0, 7).Select(i => today.AddDays(-6 + i)).ToList();

        var salesSeries = days
            .Select(day => bookedRows
                .Where(x => x.Booking.CreatedAtUtc.ToLocalTime().Date == day)
                .Sum(x => Math.Max(1, x.Booking.Quantity)))
            .ToList();

        var revenueSeries = days
            .Select(day => (int)Math.Round(bookedRows
                .Where(x => x.Booking.CreatedAtUtc.ToLocalTime().Date == day)
                .Sum(x => x.Booking.TotalAmount)))
            .ToList();

        var visitorsSeries = days
            .Select(day => bookingDates.Count(d => d.ToLocalTime().Date == day))
            .ToList();

        var totalVisitors = visitorsSeries.Sum();
        var conversion = totalVisitors == 0 ? 0 : Math.Round((ticketsSold * 100m) / Math.Max(totalVisitors, 1), 1);

        var currentStart = today.AddDays(-6);
        var previousStart = today.AddDays(-13);
        var previousEnd = today.AddDays(-7);

        var currentRows = bookedRows.Where(x => x.Booking.CreatedAtUtc.ToLocalTime().Date >= currentStart).ToList();
        var previousRows = bookedRows.Where(x =>
            x.Booking.CreatedAtUtc.ToLocalTime().Date >= previousStart &&
            x.Booking.CreatedAtUtc.ToLocalTime().Date <= previousEnd).ToList();

        var currentRevenue = currentRows.Sum(x => x.Booking.TotalAmount);
        var previousRevenue = previousRows.Sum(x => x.Booking.TotalAmount);
        var currentTickets = currentRows.Sum(x => Math.Max(1, x.Booking.Quantity));
        var previousTickets = previousRows.Sum(x => Math.Max(1, x.Booking.Quantity));
        var currentVisitors = bookingDates.Count(d => d.ToLocalTime().Date >= currentStart);
        var previousVisitors = bookingDates.Count(d =>
            d.ToLocalTime().Date >= previousStart &&
            d.ToLocalTime().Date <= previousEnd);
        var currentEvents = eventDates.Count(d => d.Date >= currentStart);
        var previousEvents = eventDates.Count(d =>
            d.Date >= previousStart &&
            d.Date <= previousEnd);
        var currentConversion = currentVisitors == 0 ? 0 : (currentTickets * 100m) / Math.Max(currentVisitors, 1);
        var previousConversion = previousVisitors == 0 ? 0 : (previousTickets * 100m) / Math.Max(previousVisitors, 1);

        static decimal Growth(decimal current, decimal previous)
        {
            if (previous <= 0) return current > 0 ? 100 : 0;
            return Math.Round(((current - previous) / previous) * 100m, 1);
        }

        var model = new OrganizerDashboardViewModel
        {
            TotalRevenue = totalRevenue,
            TicketsSold = ticketsSold,
            TotalEvents = totalEvents,
            ConversionRate = conversion,
            RevenueGrowth = Growth(currentRevenue, previousRevenue),
            TicketsGrowth = Growth(currentTickets, previousTickets),
            EventsGrowth = Growth(currentEvents, previousEvents),
            ConversionGrowth = Growth(currentConversion, previousConversion),
            SalesSeries = salesSeries,
            RevenueSeries = revenueSeries,
            VisitorsSeries = visitorsSeries,
            DateLabels = days.Select(d => d.ToString("ddd, dd MMM yyyy")).ToList(),
            RecentActivity = bookedRows
                .Take(5)
                .Select(x => new OrganizerActivityItemViewModel
                {
                    EventName = x.Event.Title,
                    UserName = GetDisplayNameFromEmail(x.Booking.UserEmail),
                    ActionText = $"Purchased {x.Booking.TicketName} Ticket",
                    Amount = x.Booking.TotalAmount,
                    TimeAgo = ToRelativeTime(x.Booking.CreatedAtUtc)
                })
                .ToList()
        };

        return View("Index", model);
    }

    public async Task<IActionResult> CreateEvent(int? id)
    {
        ViewData["Title"] = id.HasValue ? "Edit Event" : "Create Event";
        ViewData["OrgNav"] = "create";

        if (!id.HasValue)
        {
            return View(new OrganizerCreateEventViewModel { Date = DateTime.Today });
        }

        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == id.Value);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(ManageEvents));
        }

        var ticket = (await db.TicketOptions
            .Where(t => t.EventItemId == eventItem.Id)
            .ToListAsync())
            .OrderByDescending(t => t.Price)
            .FirstOrDefault();

        var model = new OrganizerCreateEventViewModel
        {
            Id = eventItem.Id,
            Title = eventItem.Title,
            Description = eventItem.Description,
            Date = eventItem.StartDateTime.Date,
            Time = eventItem.StartDateTime.ToString("HH:mm"),
            Category = eventItem.Category,
            VenueAddress = eventItem.Location,
            ImageUrl = eventItem.ImageUrl,
            TicketType = ticket is null
                ? (eventItem.Price <= 0m ? "Free" : "Paid")
                : ticket.Price <= 0m ? "Free" : ticket.Name,
            TicketPrice = ticket?.Price ?? eventItem.Price,
            AvailableQuantity = 0,
            RefundPolicy = ticket?.Features ?? string.Empty
        };

        var config = await db.OrganizerEventConfigs.FirstOrDefaultAsync(c => c.EventItemId == eventItem.Id);
        if (config is not null)
        {
            model.AvailableQuantity = config.AvailableQuantity;
            model.EarlyBirdDiscount = config.EarlyBirdDiscount;
            model.EarlyBirdPrice = config.EarlyBirdPrice;
            if (!string.IsNullOrWhiteSpace(config.RefundPolicy))
            {
                model.RefundPolicy = config.RefundPolicy;
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(OrganizerCreateEventViewModel model, IFormFile? bannerFile)
    {
        ViewData["Title"] = model.Id.HasValue ? "Edit Event" : "Create Event";
        ViewData["OrgNav"] = "create";

        if (string.IsNullOrWhiteSpace(model.Title) || model.Date is null)
        {
            ModelState.AddModelError(string.Empty, "Title and Date are required.");
            return View(model);
        }

        var timeValue = TimeSpan.Zero;
        if (!string.IsNullOrWhiteSpace(model.Time))
        {
            TimeSpan.TryParse(model.Time, out timeValue);
        }

        var start = model.Date.Value.Date.Add(timeValue);
        var price = model.TicketType.Equals("Free", StringComparison.OrdinalIgnoreCase)
            ? 0m
            : Math.Max(0m, model.TicketPrice);

        EventItem eventItem;
        if (model.Id.HasValue && model.Id.Value > 0)
        {
            var existing = await db.Events.FirstOrDefaultAsync(e => e.Id == model.Id.Value);
            if (existing is null)
            {
                return RedirectToAction(nameof(ManageEvents));
            }

            eventItem = existing;
            eventItem.Title = model.Title.Trim();
            eventItem.Category = string.IsNullOrWhiteSpace(model.Category) ? "General" : model.Category.Trim();
            eventItem.StartDateTime = start;
            eventItem.Location = string.IsNullOrWhiteSpace(model.VenueAddress) ? "Rajkot" : model.VenueAddress.Trim();
            eventItem.Price = price;
            eventItem.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl)
                ? "https://images.unsplash.com/photo-1511578314322-379afb476865?w=1200"
                : model.ImageUrl.Trim();
            eventItem.ShortDescription = model.Description?.Trim() ?? string.Empty;
            eventItem.Description = model.Description?.Trim() ?? string.Empty;
        }
        else
        {
            eventItem = new EventItem
            {
                Title = model.Title.Trim(),
                Category = string.IsNullOrWhiteSpace(model.Category) ? "General" : model.Category.Trim(),
                StartDateTime = start,
                Location = string.IsNullOrWhiteSpace(model.VenueAddress) ? "Rajkot" : model.VenueAddress.Trim(),
                Price = price,
                ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl)
                    ? "https://images.unsplash.com/photo-1511578314322-379afb476865?w=1200"
                    : model.ImageUrl.Trim(),
                Rating = 4.8,
                ReviewCount = 0,
                AttendingCount = 0,
                ShortDescription = model.Description?.Trim() ?? string.Empty,
                Description = model.Description?.Trim() ?? string.Empty
            };
            db.Events.Add(eventItem);
        }

        if (bannerFile is not null && bannerFile.Length > 0)
        {
            var ext = Path.GetExtension(bannerFile.FileName)?.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
            if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
            {
                ModelState.AddModelError(string.Empty, "Banner file must be JPG, PNG, or WEBP.");
                return View(model);
            }

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "events");
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(folder, fileName);
            await using (var stream = System.IO.File.Create(filePath))
            {
                await bannerFile.CopyToAsync(stream);
            }

            eventItem.ImageUrl = $"/uploads/events/{fileName}";
        }

        await db.SaveChangesAsync();

        var existingTicket = (await db.TicketOptions
            .Where(t => t.EventItemId == eventItem.Id)
            .ToListAsync())
            .OrderByDescending(t => t.Price)
            .FirstOrDefault();

        if (price > 0)
        {
            if (existingTicket is null)
            {
                db.TicketOptions.Add(new TicketOption
                {
                    EventItemId = eventItem.Id,
                    Name = string.IsNullOrWhiteSpace(model.TicketType) ? "Regular" : model.TicketType,
                    Price = price,
                    Features = model.RefundPolicy?.Trim() ?? string.Empty
                });
            }
            else
            {
                existingTicket.Name = string.IsNullOrWhiteSpace(model.TicketType) ? "Regular" : model.TicketType;
                existingTicket.Price = price;
                existingTicket.Features = model.RefundPolicy?.Trim() ?? string.Empty;
            }
        }
        else if (existingTicket is not null)
        {
            db.TicketOptions.Remove(existingTicket);
        }

        var config = await db.OrganizerEventConfigs.FirstOrDefaultAsync(c => c.EventItemId == eventItem.Id);
        if (config is null)
        {
            config = new OrganizerEventConfig { EventItemId = eventItem.Id };
            db.OrganizerEventConfigs.Add(config);
        }

        config.AvailableQuantity = Math.Max(0, model.AvailableQuantity);
        config.EarlyBirdDiscount = model.EarlyBirdDiscount;
        config.EarlyBirdPrice = Math.Max(0m, model.EarlyBirdPrice);
        config.RefundPolicy = model.RefundPolicy?.Trim() ?? string.Empty;

        await db.SaveChangesAsync();

        return RedirectToAction(nameof(ManageEvents));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(ManageEvents));
        }

        var tickets = await db.TicketOptions.Where(t => t.EventItemId == id).ToListAsync();
        var speakers = await db.Speakers.Where(s => s.EventItemId == id).ToListAsync();
        var reviews = await db.Reviews.Where(r => r.EventItemId == id).ToListAsync();
        var bookings = await db.Bookings.Where(b => b.EventItemId == id).ToListAsync();
        var myBookings = await db.MyBookings.Where(b => b.EventItemId == id).ToListAsync();
        var announcements = await db.OrganizerAnnouncements.Where(a => a.EventItemId == id).ToListAsync();

        if (tickets.Count > 0) db.TicketOptions.RemoveRange(tickets);
        if (speakers.Count > 0) db.Speakers.RemoveRange(speakers);
        if (reviews.Count > 0) db.Reviews.RemoveRange(reviews);
        if (bookings.Count > 0) db.Bookings.RemoveRange(bookings);
        if (myBookings.Count > 0) db.MyBookings.RemoveRange(myBookings);
        if (announcements.Count > 0) db.OrganizerAnnouncements.RemoveRange(announcements);

        db.Events.Remove(eventItem);
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(ManageEvents));
    }

    public async Task<IActionResult> ManageEvents(string? q)
    {
        ViewData["Title"] = "Manage Events";
        ViewData["OrgNav"] = "manage";

        var query = db.Events.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(e =>
                e.Title.ToLower().Contains(term)
                || e.Location.ToLower().Contains(term)
                || e.Category.ToLower().Contains(term));
        }

        var events = await query
            .OrderByDescending(e => e.StartDateTime)
            .ToListAsync();

        var bookingRows = await db.MyBookings
            .Select(b => new
            {
                b.EventItemId,
                b.Status,
                b.Quantity,
                b.TotalAmount
            })
            .ToListAsync();

        var bookingGroups = bookingRows
            .GroupBy(b => b.EventItemId)
            .Select(g => new
            {
                EventItemId = g.Key,
                Sold = g.Where(x => x.Status == "Booked").Sum(x => x.Quantity <= 0 ? 1 : x.Quantity),
                Revenue = g.Where(x => x.Status == "Booked").Sum(x => x.TotalAmount)
            })
            .ToDictionary(x => x.EventItemId, x => x);

        var model = new OrganizerManageEventsViewModel
        {
            Search = q ?? string.Empty,
            Events = events.Select(e =>
            {
                var has = bookingGroups.TryGetValue(e.Id, out var g);
                var sold = has ? g!.Sold : 0;
                var capacity = Math.Max(200, sold + 50);
                return new OrganizerManageEventCardViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    Date = e.StartDateTime,
                    Location = e.Location,
                    Category = e.Category,
                    Revenue = has ? g!.Revenue : 0m,
                    Sold = sold,
                    Capacity = capacity,
                    Status = e.StartDateTime.Date < DateTime.Today
                        ? "Completed"
                        : e.StartDateTime.Date == DateTime.Today
                            ? "Ongoing"
                            : "Upcoming"
                };
            }).ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> ViewEvent(int id)
    {
        ViewData["Title"] = "View Event";
        ViewData["OrgNav"] = "manage";

        var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (eventItem is null)
        {
            return RedirectToAction(nameof(ManageEvents));
        }

        var bookingRows = await db.MyBookings
            .Where(b => b.EventItemId == id && b.Status == "Booked")
            .Select(b => new { b.Quantity, b.TotalAmount })
            .ToListAsync();

        var sold = bookingRows.Sum(x => x.Quantity <= 0 ? 1 : x.Quantity);
        var revenue = bookingRows.Sum(x => x.TotalAmount);
        var capacity = Math.Max(200, sold + 50);

        var ticket = (await db.TicketOptions
            .Where(t => t.EventItemId == id)
            .ToListAsync())
            .OrderByDescending(t => t.Price)
            .FirstOrDefault();

        var model = new OrganizerEventDetailsViewModel
        {
            Id = eventItem.Id,
            Title = eventItem.Title,
            Date = eventItem.StartDateTime,
            Location = eventItem.Location,
            Category = eventItem.Category,
            Status = eventItem.StartDateTime.Date < DateTime.Today
                ? "Completed"
                : eventItem.StartDateTime.Date == DateTime.Today
                    ? "Ongoing"
                    : "Upcoming",
            Revenue = revenue,
            Sold = sold,
            Capacity = capacity,
            Price = ticket?.Price ?? eventItem.Price,
            Description = eventItem.Description,
            ImageUrl = eventItem.ImageUrl
        };

        return View(model);
    }

    public async Task<IActionResult> Bookings(string? q, int page = 1)
    {
        ViewData["Title"] = "Booking Management";
        ViewData["OrgNav"] = "bookings";

        const int pageSize = 10;
        if (page < 1) page = 1;

        var rows = await db.MyBookings
            .Join(db.Events, b => b.EventItemId, e => e.Id, (b, e) => new { Booking = b, Event = e })
            .OrderByDescending(x => x.Booking.CreatedAtUtc)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            rows = rows.Where(x =>
                x.Booking.BookingCode.ToLower().Contains(term)
                || x.Booking.UserEmail.ToLower().Contains(term)
                || x.Event.Title.ToLower().Contains(term)).ToList();
        }

        var mappedRows = rows.Select(x => new OrganizerBookingRowViewModel
        {
            Id = x.Booking.Id,
            BookingCode = string.IsNullOrWhiteSpace(x.Booking.BookingCode) ? $"BK{x.Booking.Id:0000}" : x.Booking.BookingCode,
            AttendeeName = GetDisplayNameFromEmail(x.Booking.UserEmail),
            AttendeeEmail = x.Booking.UserEmail,
            EventTitle = x.Event.Title,
            TicketType = x.Booking.TicketName,
            Qty = Math.Max(1, x.Booking.Quantity),
            Amount = x.Booking.TotalAmount,
            Status = x.Booking.Status,
            Date = x.Booking.CreatedAtUtc.ToLocalTime()
        }).ToList();

        var totalCount = mappedRows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var pageRows = mappedRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var model = new OrganizerBookingsViewModel
        {
            Search = q ?? string.Empty,
            Rows = pageRows,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id)
    {
        var booking = await db.MyBookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking is not null)
        {
            booking.Status = "Canceled";
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Bookings));
    }

    public async Task<IActionResult> Payments(int page = 1)
    {
        ViewData["Title"] = "Payments";
        ViewData["OrgNav"] = "payments";
        const int pageSize = 10;
        if (page < 1) page = 1;

        var rows = await db.OrganizerPaymentRecords
            .Join(db.MyBookings, p => p.MyBookingId, b => b.Id, (p, b) => new { Payment = p, Booking = b })
            .Join(db.Events, x => x.Booking.EventItemId, e => e.Id, (x, e) => new { x.Payment, x.Booking, Event = e })
            .OrderByDescending(x => x.Payment.PaidAtUtc)
            .ToListAsync();

        var mappedRows = rows.Select(x => new OrganizerPaymentRowViewModel
        {
            TransactionId = x.Payment.TransactionId,
            Date = x.Payment.PaidAtUtc.ToLocalTime(),
            EventTitle = x.Event.Title,
            Customer = GetDisplayNameFromEmail(x.Booking.UserEmail),
            Amount = x.Booking.TotalAmount,
            Method = x.Payment.Method,
            Status = x.Payment.Status
        }).ToList();

        var totalCount = mappedRows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var pageRows = mappedRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var model = new OrganizerPaymentsViewModel
        {
            TotalRevenue = rows.Where(x => x.Payment.Status == "Success").Sum(x => x.Booking.TotalAmount),
            PendingPayouts = Math.Round(rows.Where(x => x.Payment.Status == "Pending").Sum(x => x.Booking.TotalAmount), 2),
            SuccessfulTransactions = rows.Count(x => x.Payment.Status == "Success"),
            RefundRequests = rows.Count(x => x.Payment.Status == "Refund"),
            Rows = pageRows,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return View(model);
    }

    public IActionResult Reports()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public IActionResult Coupons()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateCoupon(OrganizerCouponsViewModel model)
    {
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteCoupon(int id)
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Announcements()
    {
        ViewData["Title"] = "Announcements";
        ViewData["OrgNav"] = "announcements";

        var events = await db.Events.OrderByDescending(e => e.StartDateTime).ToListAsync();
        var announcementRows = await db.OrganizerAnnouncements
            .GroupJoin(
                db.Events,
                a => a.EventItemId,
                e => e.Id,
                (a, es) => new { Ann = a, Event = es.FirstOrDefault() }
            )
            .OrderByDescending(x => x.Ann.CreatedAtUtc)
            .Select(x => new
            {
                Id = x.Ann.Id,
                Title = x.Ann.Title,
                EventTitle = x.Event != null ? x.Event.Title : "General",
                CreatedAtUtc = x.Ann.CreatedAtUtc,
                Message = x.Ann.Message
            })
            .ToListAsync();

        var announcements = announcementRows
            .Select(x => new OrganizerAnnouncementListItemViewModel
            {
                Id = x.Id,
                Title = x.Title,
                EventTitle = x.EventTitle,
                Date = x.CreatedAtUtc.ToLocalTime(),
                Message = x.Message
            })
            .ToList();

        var model = new OrganizerAnnouncementsViewModel
        {
            Events = events,
            Announcements = announcements
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAnnouncement(OrganizerAnnouncementsViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.Title) && !string.IsNullOrWhiteSpace(model.Message))
        {
            var announcement = new OrganizerAnnouncement
            {
                EventItemId = model.EventItemId,
                Title = model.Title.Trim(),
                Message = model.Message.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
            db.OrganizerAnnouncements.Add(announcement);

            var targetEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string eventTitle = "General";

            if (model.EventItemId.HasValue && model.EventItemId.Value > 0)
            {
                var eventItem = await db.Events.FirstOrDefaultAsync(e => e.Id == model.EventItemId.Value);
                if (eventItem is not null)
                {
                    eventTitle = eventItem.Title;
                }

                var bookedEmails = await db.MyBookings
                    .Where(b => b.EventItemId == model.EventItemId.Value && b.Status == "Booked")
                    .Select(b => b.UserEmail)
                    .Distinct()
                    .ToListAsync();

                var savedEmails = await db.Bookings
                    .Where(b => b.EventItemId == model.EventItemId.Value && b.IsSaved)
                    .Select(b => b.UserEmail)
                    .Distinct()
                    .ToListAsync();

                foreach (var email in bookedEmails.Concat(savedEmails))
                {
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        targetEmails.Add(email.Trim().ToLowerInvariant());
                    }
                }
            }
            else
            {
                var allAttendUsers = await db.Users
                    .Where(u => u.Role.ToLower() == "attend")
                    .Select(u => u.Email)
                    .ToListAsync();

                foreach (var email in allAttendUsers)
                {
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        targetEmails.Add(email.Trim().ToLowerInvariant());
                    }
                }
            }

            if (targetEmails.Count == 0)
            {
                targetEmails.Add("attend@eventify.com");
            }

            foreach (var email in targetEmails)
            {
                db.AttendNotifications.Add(new AttendNotification
                {
                    UserEmail = email,
                    Title = announcement.Title,
                    Message = model.EventItemId.HasValue
                        ? $"[{eventTitle}] {announcement.Message}"
                        : announcement.Message,
                    Kind = "announcement",
                    IsRead = false,
                    CreatedAtUtc = announcement.CreatedAtUtc
                });
            }

            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Announcements));
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
        ViewData["OrgNav"] = "profile";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(OrganizerProfileSettingsViewModel input, IFormFile? profilePhoto)
    {
        var organizerEmail = ResolveOrganizerEmail();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == organizerEmail);
        if (user is null)
        {
            return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", error = "User not found." });
        }

        var settings = await db.OrganizerProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == organizerEmail);
        if (settings is null)
        {
            settings = new OrganizerProfileSetting { UserEmail = organizerEmail };
            db.OrganizerProfileSettings.Add(settings);
        }

        user.FullName = string.IsNullOrWhiteSpace(input.FullName) ? user.FullName : input.FullName.Trim();
        settings.PhoneNumber = input.PhoneNumber?.Trim() ?? string.Empty;
        settings.Location = input.Location?.Trim() ?? string.Empty;
        settings.DateOfBirth = input.DateOfBirth;
        settings.Bio = input.Bio?.Trim() ?? string.Empty;

        if (profilePhoto is not null && profilePhoto.Length > 0)
        {
            var ext = Path.GetExtension(profilePhoto.FileName)?.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
            if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
            {
                return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", error = "Profile photo must be JPG, PNG, or WEBP." });
            }

            if (!string.IsNullOrWhiteSpace(settings.ProfilePhotoPath) &&
                settings.ProfilePhotoPath.StartsWith("/uploads/profiles/", StringComparison.OrdinalIgnoreCase))
            {
                var oldFileName = Path.GetFileName(settings.ProfilePhotoPath);
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles", oldFileName);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }

            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(folder, fileName);
            await using (var stream = System.IO.File.Create(filePath))
            {
                await profilePhoto.CopyToAsync(stream);
            }

            settings.ProfilePhotoPath = $"/uploads/profiles/{fileName}";
        }

        await db.SaveChangesAsync();
        await RoleDatabaseMirror.MirrorUserAsync(config, user);
        HttpContext.Session.SetString("UserFullName", user.FullName ?? "Organizer User");
        HttpContext.Session.SetString("UserPhotoPath", settings.ProfilePhotoPath ?? string.Empty);

        return RedirectToAction(nameof(ProfileSettings), new { tab = "edit", message = "Profile updated." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotificationPreferences(OrganizerProfileSettingsViewModel input)
    {
        var organizerEmail = ResolveOrganizerEmail();
        var settings = await db.OrganizerProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == organizerEmail);
        if (settings is null)
        {
            settings = new OrganizerProfileSetting { UserEmail = organizerEmail };
            db.OrganizerProfileSettings.Add(settings);
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
    public async Task<IActionResult> ChangePassword(OrganizerProfileSettingsViewModel input)
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

        var organizerEmail = ResolveOrganizerEmail();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == organizerEmail);
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
        user.PasswordText = input.NewPassword;
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await RoleDatabaseMirror.MirrorUserAsync(config, user);

        return RedirectToAction(nameof(ProfileSettings), new { tab = "password", message = "Password updated." });
    }

    private async Task<OrganizerProfileSettingsViewModel> GetProfileSettingsModelAsync(string activeTab)
    {
        var organizerEmail = ResolveOrganizerEmail();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == organizerEmail);
        if (user is null)
        {
            user = new UserAccount
            {
                FullName = "Meet Modhvadiya",
                Email = organizerEmail,
                PasswordHash = PasswordHasher.Hash("Organizer@2026!Go"),
                PasswordText = "Organizer@2026!Go",
                Role = "organizer"
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var settings = await db.OrganizerProfileSettings.FirstOrDefaultAsync(s => s.UserEmail == organizerEmail);
        if (settings is null)
        {
            settings = new OrganizerProfileSetting
            {
                UserEmail = organizerEmail,
                PhoneNumber = "+91 90000 00000",
                Location = "Rajkot, Gujarat"
            };
            db.OrganizerProfileSettings.Add(settings);
            await db.SaveChangesAsync();
        }

        HttpContext.Session.SetString("UserFullName", user.FullName ?? "Organizer User");
        HttpContext.Session.SetString("UserPhotoPath", settings.ProfilePhotoPath ?? string.Empty);

        return new OrganizerProfileSettingsViewModel
        {
            ActiveTab = activeTab,
            FullName = user.FullName ?? "Organizer User",
            Email = user.Email,
            ProfilePhotoPath = settings.ProfilePhotoPath ?? string.Empty,
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

    private static string GetDisplayNameFromEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return "User";
        }

        var namePart = email.Split('@')[0].Replace('.', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(namePart)
            ? "User"
            : char.ToUpper(namePart[0]) + namePart[1..];
    }

    private static string ToRelativeTime(DateTime utcDate)
    {
        var span = DateTime.UtcNow - utcDate;
        if (span.TotalMinutes < 60)
        {
            var mins = Math.Max(1, (int)Math.Round(span.TotalMinutes));
            return $"{mins} min ago";
        }

        if (span.TotalHours < 24)
        {
            var hrs = (int)Math.Round(span.TotalHours);
            return $"{hrs} hour{(hrs == 1 ? "" : "s")} ago";
        }

        var days = (int)Math.Round(span.TotalDays);
        return $"{days} day{(days == 1 ? "" : "s")} ago";
    }

    private string ResolveOrganizerEmail()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email.Trim().ToLowerInvariant();
        }

        return "organizer@eventify.com";
    }
}

