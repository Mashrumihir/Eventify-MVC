using System.Diagnostics;
using Eventify.Data;
using Eventify.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

namespace Eventify.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly EventifyDbContext _db;

        public HomeController(ILogger<HomeController> logger, EventifyDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<IActionResult> Index(string? location, DateTime? date, string? category)
        {
            var normalizedLocation = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
            var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

            var categories = await _db.Events
                .Select(e => e.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var query = _db.Events.AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedLocation))
            {
                var locationTerm = normalizedLocation.ToLower();
                query = query.Where(e => e.Location.ToLower().Contains(locationTerm));
            }

            if (date.HasValue)
            {
                var selectedDate = date.Value.Date;
                query = query.Where(e => e.StartDateTime.Date == selectedDate);
            }

            if (!string.IsNullOrWhiteSpace(normalizedCategory))
            {
                query = query.Where(e => e.Category == normalizedCategory);
            }

            var events = await query
                .OrderBy(e => e.StartDateTime)
                .Take(6)
                .ToListAsync();

            ViewBag.Events = events;
            ViewBag.Categories = categories;
            ViewBag.SearchLocation = normalizedLocation ?? string.Empty;
            ViewBag.SearchDate = date?.ToString("yyyy-MM-dd") ?? string.Empty;
            ViewBag.SelectedCategory = normalizedCategory ?? string.Empty;
            ViewBag.HasSearch = !string.IsNullOrWhiteSpace(normalizedLocation) || date.HasValue || !string.IsNullOrWhiteSpace(normalizedCategory);

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult AboutUs()
        {
            return View("AboutUs/Index");
        }

        public IActionResult PartnerEvents()
        {
            return View("PartnerEvents/Index");
        }

        public IActionResult FAQ()
        {
            return View("FAQ/Index");
        }

        public IActionResult Pricing()
        {
            return View("Pricing/Index");
        }
        public IActionResult Contact()
        {
            return View("Contact/Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitContact(string? fullName, string? email, string? subject, string? message)
        {
            var normalizedName = (fullName ?? string.Empty).Trim();
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedSubject = string.IsNullOrWhiteSpace(subject) ? "General Query" : subject.Trim();
            var normalizedMessage = (message ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(normalizedMessage))
            {
                TempData["ContactMessageStatus"] = "Please enter your name, email, and message.";
                TempData["ContactMessageType"] = "error";
                return RedirectToAction(nameof(Contact));
            }

            if (!IsValidEmail(normalizedEmail))
            {
                TempData["ContactMessageStatus"] = "Please enter a valid email address.";
                TempData["ContactMessageType"] = "error";
                return RedirectToAction(nameof(Contact));
            }

            _db.AdminContactMessages.Add(new AdminContactMessage
            {
                FullName = normalizedName,
                Email = normalizedEmail,
                Subject = normalizedSubject,
                Message = normalizedMessage,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            TempData["ContactMessageStatus"] = "Message sent successfully. Our team will contact you soon.";
            TempData["ContactMessageType"] = "success";
            return RedirectToAction(nameof(Contact));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubscribeNewsletter(string? email)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                TempData["NewsletterMessage"] = "Please enter your email address.";
                TempData["NewsletterMessageType"] = "error";
                return RedirectToAction(nameof(Index), null, null, "newsletter");
            }

            if (!IsValidEmail(normalizedEmail))
            {
                TempData["NewsletterMessage"] = "Please enter a valid email address.";
                TempData["NewsletterMessageType"] = "error";
                return RedirectToAction(nameof(Index), null, null, "newsletter");
            }

            var alreadySubscribed = await _db.AdminNewsletterSubscribers
                .AnyAsync(subscriber => subscriber.Email == normalizedEmail);

            if (alreadySubscribed)
            {
                TempData["NewsletterMessage"] = $"{normalizedEmail} is already subscribed.";
                TempData["NewsletterMessageType"] = "success";
                return RedirectToAction(nameof(Index), null, null, "newsletter");
            }

            _db.AdminNewsletterSubscribers.Add(new AdminNewsletterSubscriber
            {
                Email = normalizedEmail,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            TempData["NewsletterMessage"] = "Thanks for subscribing. Your email is now saved.";
            TempData["NewsletterMessageType"] = "success";
            return RedirectToAction(nameof(Index), null, null, "newsletter");
        }

        public IActionResult PrivacyPolicy()
        {
            return View("PrivacyPolicy/Index");
        }
        public IActionResult TermsOfService()
        {
            return View("TermsOfService/Index");
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}















