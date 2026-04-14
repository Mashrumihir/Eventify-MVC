using Eventify.Data;
using Eventify.Models;
using Eventify.ViewModels;
using Eventify.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Eventify.Controllers;

public class AdminController(EventifyDbContext db, IConfiguration config, IWebHostEnvironment env) : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Dashboard()
    {
        ViewData["Title"] = "Admin Dashboard";
        ViewData["AdminNav"] = "dashboard";

        await EnsureAdminRecordsConnectedAsync();

        var users = await db.Users
            .AsNoTracking()
            .ToListAsync();
        var totalUsers = users.Count;
        var organizers = users.Count(u => string.Equals(u.Role, "organizer", StringComparison.OrdinalIgnoreCase));
        var eventsCount = await db.Events.CountAsync();
        var revenue = await db.MyBookings
            .Where(b => b.Status == "Booked")
            .SumAsync(b => (decimal?)b.TotalAmount) ?? 0m;

        var nowUtc = DateTime.UtcNow;
        var currentStartUtc = nowUtc.AddDays(-30);
        var previousStartUtc = nowUtc.AddDays(-60);

        var currentUsers = users.Count(u => u.CreatedAtUtc >= currentStartUtc);
        var previousUsers = users.Count(u => u.CreatedAtUtc >= previousStartUtc && u.CreatedAtUtc < currentStartUtc);

        var currentOrganizers = users.Count(u =>
            string.Equals(u.Role, "organizer", StringComparison.OrdinalIgnoreCase) &&
            u.CreatedAtUtc >= currentStartUtc);
        var previousOrganizers = users.Count(u =>
            string.Equals(u.Role, "organizer", StringComparison.OrdinalIgnoreCase) &&
            u.CreatedAtUtc >= previousStartUtc &&
            u.CreatedAtUtc < currentStartUtc);

        var nowLocal = DateTime.Now;
        var currentStartLocal = nowLocal.AddDays(-30);
        var previousStartLocal = nowLocal.AddDays(-60);
        var allEvents = await db.Events.ToListAsync();
        var currentEvents = allEvents.Count(e => e.StartDateTime >= currentStartLocal);
        var previousEvents = allEvents.Count(e => e.StartDateTime >= previousStartLocal && e.StartDateTime < currentStartLocal);

        var bookedRows = await db.MyBookings
            .Where(b => b.Status == "Booked")
            .Select(b => new { b.TotalAmount, b.CreatedAtUtc })
            .ToListAsync();
        var currentRevenue = bookedRows
            .Where(b => b.CreatedAtUtc >= currentStartUtc)
            .Sum(b => b.TotalAmount);
        var previousRevenue = bookedRows
            .Where(b => b.CreatedAtUtc >= previousStartUtc && b.CreatedAtUtc < currentStartUtc)
            .Sum(b => b.TotalAmount);

        var usersTrend = ComputeGrowthPercent(currentUsers, previousUsers);
        var organizersTrend = ComputeGrowthPercent(currentOrganizers, previousOrganizers);
        var eventsTrend = ComputeGrowthPercent(currentEvents, previousEvents);
        var revenueTrend = ComputeGrowthPercent(currentRevenue, previousRevenue);

        await UpsertDashboardTrendAsync("users", usersTrend);
        await UpsertDashboardTrendAsync("organizers", organizersTrend);
        await UpsertDashboardTrendAsync("events", eventsTrend);
        await UpsertDashboardTrendAsync("revenue", revenueTrend);

        var pendingApprovals = await db.AdminOrganizerApplications
            .Where(a => a.Status == "pending")
            .OrderBy(a => a.AppliedOnUtc)
            .Take(5)
            .Select(a => new AdminPendingApprovalItemViewModel
            {
                Id = a.Id,
                Name = a.OrganizationName,
                Type = "Organizer",
                DateText = a.AppliedOnUtc.ToLocalTime().ToString("MMM dd, yyyy")
            })
            .ToListAsync();

        var pendingEvents = await db.AdminModerationEvents
            .Where(e => e.Status == "pending")
            .OrderBy(e => e.CreatedAtUtc)
            .Take(Math.Max(0, 5 - pendingApprovals.Count))
            .Select(e => new AdminPendingApprovalItemViewModel
            {
                Id = e.Id,
                Name = e.EventTitle,
                Type = "Event",
                DateText = e.EventDate.ToString("MMM dd, yyyy")
            })
            .ToListAsync();

        pendingApprovals.AddRange(pendingEvents);

        var subscribers = await db.AdminNewsletterSubscribers
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(6)
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            TotalOrganizers = organizers,
            TotalEvents = eventsCount,
            Revenue = revenue,
            UsersTrendPercent = usersTrend,
            OrganizersTrendPercent = organizersTrend,
            EventsTrendPercent = eventsTrend,
            RevenueTrendPercent = revenueTrend,
            NewsletterSubscribersCount = await db.AdminNewsletterSubscribers.CountAsync(),
            PendingApprovals = pendingApprovals,
            NewsletterSubscribers = subscribers.Select(x => new AdminNewsletterSubscriberItemViewModel
            {
                Id = x.Id,
                Email = x.Email,
                AddedOnText = x.CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt")
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNewsletterSubscriber(string email, string? returnUrl = null)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var redirectUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? Url.Action(nameof(Dashboard))
            : returnUrl;

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            TempData["AdminActionMessage"] = "Enter an email address.";
            return LocalRedirect(redirectUrl!);
        }

        if (!IsValidEmail(normalizedEmail))
        {
            TempData["AdminActionMessage"] = "Enter a valid email address.";
            return LocalRedirect(redirectUrl!);
        }

        var exists = await db.AdminNewsletterSubscribers.AnyAsync(x => x.Email == normalizedEmail);
        if (exists)
        {
            TempData["AdminActionMessage"] = $"{normalizedEmail} is already subscribed.";
            return LocalRedirect(redirectUrl!);
        }

        db.AdminNewsletterSubscribers.Add(new AdminNewsletterSubscriber
        {
            Email = normalizedEmail,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        TempData["AdminActionMessage"] = $"{normalizedEmail} subscribed successfully.";
        return LocalRedirect(redirectUrl!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNewsletterSubscriber(int id, string? returnUrl = null)
    {
        var redirectUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? Url.Action(nameof(NewsletterSubscribers))
            : returnUrl;

        var subscriber = await db.AdminNewsletterSubscribers.FirstOrDefaultAsync(x => x.Id == id);
        if (subscriber is null)
        {
            TempData["AdminActionMessage"] = "Subscriber not found.";
            return LocalRedirect(redirectUrl!);
        }

        db.AdminNewsletterSubscribers.Remove(subscriber);
        await db.SaveChangesAsync();

        TempData["AdminActionMessage"] = $"{subscriber.Email} deleted successfully.";
        return LocalRedirect(redirectUrl!);
    }

    private async Task UpsertDashboardTrendAsync(string metric, decimal value)
    {
        var row = await db.AdminDashboardTrends.FirstOrDefaultAsync(x => x.Metric == metric);
        if (row is null)
        {
            db.AdminDashboardTrends.Add(new AdminDashboardTrend
            {
                Metric = metric,
                PercentChange = value
            });
        }
        else
        {
            row.PercentChange = value;
        }

        await db.SaveChangesAsync();
    }

    private static decimal ComputeGrowthPercent(decimal current, decimal previous)
    {
        if (previous <= 0m)
        {
            return current > 0m ? 100m : 0m;
        }

        return Math.Round(((current - previous) / previous) * 100m, 1);
    }

    public async Task<IActionResult> UserManagement(string? q, string role = "all")
    {
        ViewData["Title"] = "User Management";
        ViewData["AdminNav"] = "users";
        const string hiddenEmail = "attend@eventify.com";

        var bookingAgg = await db.MyBookings
            .GroupBy(b => b.UserEmail)
            .Select(g => new { Email = g.Key, Count = g.Count() })
            .ToListAsync();
        var bookingMap = bookingAgg.ToDictionary(x => x.Email, x => x.Count, StringComparer.OrdinalIgnoreCase);
        var attendPhotoMap = await db.AttendProfileSettings
            .Where(x => !string.IsNullOrWhiteSpace(x.ProfilePhotoPath))
            .ToDictionaryAsync(x => x.UserEmail, x => x.ProfilePhotoPath, StringComparer.OrdinalIgnoreCase);
        var organizerPhotoMap = await db.OrganizerProfileSettings
            .Where(x => !string.IsNullOrWhiteSpace(x.ProfilePhotoPath))
            .ToDictionaryAsync(x => x.UserEmail, x => x.ProfilePhotoPath, StringComparer.OrdinalIgnoreCase);

        role = NormalizeUserRoleFilter(role);

        var users = await db.Users
            .Where(u => u.Email.ToLower() != hiddenEmail)
            .OrderByDescending(u => u.CreatedAtUtc)
            .ToListAsync();
        var attendCount = users.Count(u => string.Equals(u.Role, "attend", StringComparison.OrdinalIgnoreCase));
        var organizerCount = users.Count(u => string.Equals(u.Role, "organizer", StringComparison.OrdinalIgnoreCase));
        var adminCount = users.Count(u => string.Equals(u.Role, "admin", StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(role, "all", StringComparison.OrdinalIgnoreCase))
        {
            users = users.Where(u => string.Equals(u.Role, role, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            users = users.Where(u =>
                u.FullName.ToLowerInvariant().Contains(term)
                || u.Email.ToLowerInvariant().Contains(term)).ToList();
        }

        var model = new AdminUserManagementViewModel
        {
            Search = q ?? string.Empty,
            ActiveRole = role,
            AttendCount = attendCount,
            OrganizerCount = organizerCount,
            AdminCount = adminCount,
            Users = users.Select(u => new AdminUserRowViewModel
            {
                Id = u.Id,
                Initials = GetInitials(u.FullName),
                FullName = u.FullName,
                Email = u.Email,
                PasswordText = u.PasswordText,
                Role = u.Role,
                Status = string.Equals(u.Role, "blocked", StringComparison.OrdinalIgnoreCase) ? "blocked" : "active",
                JoinDate = u.CreatedAtUtc.ToLocalTime(),
                Bookings = bookingMap.TryGetValue(u.Email, out var count) ? count : 0,
                ProfilePhotoPath = ResolveUserPhotoPath(u.Role, u.Email, attendPhotoMap, organizerPhotoMap)
            }).ToList()
        };

        return View(model);
    }

    private static string ResolveUserPhotoPath(
        string role,
        string email,
        IReadOnlyDictionary<string, string> attendPhotoMap,
        IReadOnlyDictionary<string, string> organizerPhotoMap)
    {
        if (string.Equals(role, "attend", StringComparison.OrdinalIgnoreCase) &&
            attendPhotoMap.TryGetValue(email, out var attendPhoto))
        {
            return attendPhoto;
        }

        if (string.Equals(role, "organizer", StringComparison.OrdinalIgnoreCase) &&
            organizerPhotoMap.TryGetValue(email, out var organizerPhoto))
        {
            return organizerPhoto;
        }

        return string.Empty;
    }

    private async Task EnsureAdminRecordsConnectedAsync()
    {
        var hasChanges = false;

        var organizerUsers = await db.Users
            .Where(u => u.Role.ToLower() == "organizer")
            .ToListAsync();
        var appEmailRows = await db.AdminOrganizerApplications
            .Select(a => a.Email.ToLower())
            .ToListAsync();
        var appEmailSet = appEmailRows.ToHashSet();

        foreach (var organizer in organizerUsers)
        {
            var organizerEmail = organizer.Email.Trim().ToLowerInvariant();
            if (appEmailSet.Contains(organizerEmail))
            {
                continue;
            }

            db.AdminOrganizerApplications.Add(new AdminOrganizerApplication
            {
                OrganizationName = string.IsNullOrWhiteSpace(organizer.FullName) ? "Organizer" : organizer.FullName,
                Email = organizer.Email,
                AppliedOnUtc = organizer.CreatedAtUtc == default ? DateTime.UtcNow : organizer.CreatedAtUtc,
                BusinessLicenseSubmitted = true,
                TaxIdSubmitted = true,
                IdVerificationSubmitted = true,
                Status = "pending"
            });
            hasChanges = true;
        }

        var events = await db.Events.ToListAsync();
        var moderationEventIdRows = await db.AdminModerationEvents
            .Where(e => e.EventItemId.HasValue)
            .Select(e => e.EventItemId!.Value)
            .ToListAsync();
        var moderationEventIdSet = moderationEventIdRows.ToHashSet();

        foreach (var ev in events)
        {
            if (moderationEventIdSet.Contains(ev.Id))
            {
                continue;
            }

            db.AdminModerationEvents.Add(new AdminEventModeration
            {
                EventItemId = ev.Id,
                EventTitle = ev.Title,
                OrganizerName = organizerUsers.FirstOrDefault()?.FullName ?? "Organizer",
                EventDate = ev.StartDateTime,
                Location = ev.Location,
                Capacity = 200,
                Price = ev.Price,
                Status = "pending",
                CreatedAtUtc = DateTime.UtcNow
            });
            hasChanges = true;
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string role = "attend", string? fullName = null, string? email = null, string? password = null, string? returnUrl = null)
    {
        role = role.Trim().ToLowerInvariant();
        if (role != "attend" && role != "organizer" && role != "admin")
        {
            role = "attend";
        }

        var nameInput = (fullName ?? string.Empty).Trim();
        var emailInput = (email ?? string.Empty).Trim().ToLowerInvariant();
        var passwordInput = (password ?? string.Empty).Trim();

        string finalName;
        string finalEmail;
        string finalPassword;

        if (!string.IsNullOrWhiteSpace(nameInput) || !string.IsNullOrWhiteSpace(emailInput) || !string.IsNullOrWhiteSpace(passwordInput))
        {
            if (string.IsNullOrWhiteSpace(emailInput) || string.IsNullOrWhiteSpace(passwordInput))
            {
                TempData["UserActionMessage"] = "Enter email and password.";
                return RedirectToAction(nameof(UserManagement), new { role });
            }

            if (await db.Users.AnyAsync(u => u.Email == emailInput))
            {
                TempData["UserActionMessage"] = $"Email already exists: {emailInput}";
                return RedirectToAction(nameof(UserManagement), new { role });
            }

            finalName = !string.IsNullOrWhiteSpace(nameInput)
                ? nameInput
                : DeriveDisplayNameFromEmail(emailInput, role);
            finalEmail = emailInput;
            finalPassword = passwordInput;
        }
        else
        {
            var index = 1;
            string generatedEmail;
            var prefix = role switch
            {
                "organizer" => "organizer.user",
                "admin" => "admin.user",
                _ => "attend.user"
            };

            do
            {
                generatedEmail = $"{prefix}{index}@eventify.com";
                index++;
            } while (await db.Users.AnyAsync(u => u.Email == generatedEmail));

            finalName = $"{char.ToUpper(role[0])}{role[1..]} User {index - 1}";
            finalEmail = generatedEmail;
            finalPassword = role switch
            {
                "organizer" => "Organizer@2026!Go",
                "admin" => "Admin@2026!Go",
                _ => "Attend@2026!Go"
            };
        }

        var user = new UserAccount
        {
            FullName = finalName,
            Email = finalEmail,
            PasswordHash = Eventify.Utilities.PasswordHasher.Hash(finalPassword),
            PasswordText = finalPassword,
            Role = role,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        await RoleDatabaseMirror.MirrorUserAsync(config, user);
        TempData["UserActionMessage"] = $"{user.FullName} ({role}) created.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(UserManagement), new { role });
    }

    private static string DeriveDisplayNameFromEmail(string email, string role)
    {
        var localPart = email.Split('@', 2)[0];
        var cleaned = Regex.Replace(localPart, @"[._\-]+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return $"{char.ToUpper(role[0])}{role[1..]} User";
        }

        var words = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => $"{char.ToUpper(word[0])}{word[1..]}")
            .ToArray();

        return words.Length == 0
            ? $"{char.ToUpper(role[0])}{role[1..]} User"
            : string.Join(' ', words);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ShowUser(int id, string? returnUrl = null)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            TempData["UserActionMessage"] = $"{user.FullName} | {user.Email} | role: {user.Role}";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(UserManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockUser(int id, string? returnUrl = null)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            var isBlocked = string.Equals(user.Role, "blocked", StringComparison.OrdinalIgnoreCase);
            user.Role = isBlocked ? "attend" : "blocked";
            await db.SaveChangesAsync();
            TempData["UserActionMessage"] = isBlocked
                ? $"{user.FullName} unblocked."
                : $"{user.FullName} blocked.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(UserManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id, string? returnUrl = null)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            var name = user.FullName;
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            TempData["UserActionMessage"] = $"{name} deleted.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(UserManagement));
    }

    public async Task<IActionResult> OrganizerApproval(string status = "pending")
    {
        ViewData["Title"] = "Organizer Approval";
        ViewData["AdminNav"] = "approvals";

        status = NormalizeStatus(status, "pending");

        var all = await db.AdminOrganizerApplications.OrderByDescending(a => a.AppliedOnUtc).ToListAsync();
        var model = new AdminOrganizerApprovalViewModel
        {
            ActiveTab = status,
            PendingCount = all.Count(x => x.Status == "pending"),
            ApprovedCount = all.Count(x => x.Status == "approved"),
            RejectedCount = all.Count(x => x.Status == "rejected"),
            Applications = all
                .Where(x => x.Status == status)
                .Select(x => new AdminOrganizerApplicationCardViewModel
                {
                    Id = x.Id,
                    OrganizationName = x.OrganizationName,
                    Email = x.Email,
                    AppliedOn = x.AppliedOnUtc.ToLocalTime().Date,
                    BusinessLicenseSubmitted = x.BusinessLicenseSubmitted,
                    TaxIdSubmitted = x.TaxIdSubmitted,
                    IdVerificationSubmitted = x.IdVerificationSubmitted,
                    Status = x.Status
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideOrganizer(int id, string decision, string? returnUrl = null)
    {
        var app = await db.AdminOrganizerApplications.FirstOrDefaultAsync(x => x.Id == id);
        if (app is not null)
        {
            app.Status = NormalizeStatus(decision, app.Status);
            await db.SaveChangesAsync();
            TempData["AdminActionMessage"] = $"{app.OrganizationName} {app.Status}.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(OrganizerApproval));
    }

    public async Task<IActionResult> EventModeration(string status = "pending")
    {
        ViewData["Title"] = "Event Moderation";
        ViewData["AdminNav"] = "moderation";

        status = NormalizeStatus(status, "pending");

        var all = await db.AdminModerationEvents.OrderByDescending(e => e.EventDate).ToListAsync();
        var model = new AdminEventModerationViewModel
        {
            ActiveTab = status,
            PendingCount = all.Count(x => x.Status == "pending"),
            ApprovedCount = all.Count(x => x.Status == "approved"),
            FeaturedCount = all.Count(x => x.Status == "featured"),
            RejectedCount = all.Count(x => x.Status == "rejected"),
            Events = all
                .Where(x => x.Status == status)
                .Select(x => new AdminEventModerationCardViewModel
                {
                    Id = x.Id,
                    Initials = GetInitials(x.EventTitle),
                    EventTitle = x.EventTitle,
                    OrganizerName = x.OrganizerName,
                    EventDate = x.EventDate,
                    Location = x.Location,
                    Capacity = x.Capacity,
                    Price = x.Price,
                    Status = x.Status
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideEvent(int id, string decision, string? returnUrl = null)
    {
        var entity = await db.AdminModerationEvents.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is not null)
        {
            entity.Status = decision switch
            {
                "approve" => "approved",
                "feature" => "featured",
                "reject" => "rejected",
                _ => entity.Status
            };
            await db.SaveChangesAsync();
            TempData["AdminActionMessage"] = $"{entity.EventTitle} {entity.Status}.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(EventModeration));
    }

    public async Task<IActionResult> SystemSettings()
    {
        ViewData["Title"] = "System Settings";
        ViewData["AdminNav"] = "settings";

        var model = new AdminSystemSettingsViewModel
        {
            Categories = await db.AdminCategories.OrderByDescending(x => x.EventCount).ToListAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(int? id, string name, string description, string icon = "bi-tag", int eventCount = 0)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        var trimmedDescription = (description ?? string.Empty).Trim();
        var trimmedIcon = string.IsNullOrWhiteSpace(icon) ? "bi-tag" : icon.Trim();
        var normalizedEventCount = Math.Max(0, eventCount);

        if (string.IsNullOrWhiteSpace(trimmedName) || string.IsNullOrWhiteSpace(trimmedDescription))
        {
            TempData["AdminActionMessage"] = "Enter both category name and description.";
            return RedirectToAction(nameof(SystemSettings));
        }

        var duplicateExists = await db.AdminCategories.AnyAsync(x =>
            x.Id != (id ?? 0) &&
            x.Name.ToLower() == trimmedName.ToLower());

        if (duplicateExists)
        {
            TempData["AdminActionMessage"] = "A category with this name already exists.";
            return RedirectToAction(nameof(SystemSettings));
        }

        if (id.HasValue && id.Value > 0)
        {
            var existing = await db.AdminCategories.FirstOrDefaultAsync(x => x.Id == id.Value);
            if (existing is null)
            {
                TempData["AdminActionMessage"] = "Category not found.";
                return RedirectToAction(nameof(SystemSettings));
            }

            existing.Name = trimmedName;
            existing.Description = trimmedDescription;
            existing.Icon = trimmedIcon;
            existing.EventCount = normalizedEventCount;

            TempData["AdminActionMessage"] = $"{trimmedName} updated successfully.";
        }
        else
        {
            db.AdminCategories.Add(new AdminCategory
            {
                Name = trimmedName,
                Description = trimmedDescription,
                Icon = trimmedIcon,
                EventCount = normalizedEventCount,
                CreatedAtUtc = DateTime.UtcNow
            });

            TempData["AdminActionMessage"] = $"{trimmedName} added successfully.";
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(SystemSettings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrganizerApplication(string organizationName, string email, string status = "pending")
    {
        var org = (organizationName ?? string.Empty).Trim();
        var mail = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(org) || string.IsNullOrWhiteSpace(mail))
        {
            TempData["AdminActionMessage"] = "Enter organization name and email.";
            return RedirectToAction(nameof(OrganizerApproval), new { status = "pending" });
        }

        var normalizedStatus = NormalizeStatus(status, "pending");
        db.AdminOrganizerApplications.Add(new AdminOrganizerApplication
        {
            OrganizationName = org,
            Email = mail,
            AppliedOnUtc = DateTime.UtcNow,
            BusinessLicenseSubmitted = true,
            TaxIdSubmitted = true,
            IdVerificationSubmitted = true,
            Status = normalizedStatus
        });

        await db.SaveChangesAsync();
        TempData["AdminActionMessage"] = $"Organizer application created as {normalizedStatus}.";
        return RedirectToAction(nameof(OrganizerApproval), new { status = normalizedStatus });
    }

    public async Task<IActionResult> Notifications()
    {
        ViewData["Title"] = "Notifications";
        ViewData["AdminNav"] = "notifications";

        var attend = await db.AttendNotifications
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync();
        var announce = await db.OrganizerAnnouncements
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(8)
            .ToListAsync();

        var importantAttend = attend
            .Where(n => n.Kind == "booking" || n.Kind == "reminder")
            .Take(2)
            .ToList();

        var items = importantAttend.Select(n => new AdminNotificationItemViewModel
        {
            Type = n.Kind switch
            {
                "booking" => "booking",
                "reminder" => "reminder",
                "payment" => "payment",
                "cancellation" => "cancellation",
                _ => "announcement"
            },
            Title = n.Title,
            Message = n.Message,
            TimeAgo = ToRelative(n.CreatedAtUtc),
            SortDateUtc = n.CreatedAtUtc
        }).ToList();

        items.AddRange(announce.Select(a => new AdminNotificationItemViewModel
        {
            Type = "announcement",
            Title = a.Title,
            Message = a.Message,
            TimeAgo = ToRelative(a.CreatedAtUtc),
            SortDateUtc = a.CreatedAtUtc
        }));

        var model = new AdminNotificationsViewModel
        {
            Items = items
                .OrderByDescending(x => x.SortDateUtc)
                .Take(6)
                .ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> NewsletterSubscribers()
    {
        ViewData["Title"] = "Newsletter Subscribers";
        ViewData["AdminNav"] = "newsletter";

        var subscribers = await db.AdminNewsletterSubscribers
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var model = new AdminNewsletterSubscribersViewModel
        {
            TotalSubscribers = subscribers.Count,
            Subscribers = subscribers.Select(x => new AdminNewsletterSubscriberItemViewModel
            {
                Id = x.Id,
                Email = x.Email,
                AddedOnText = x.CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt")
            }).ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> ContactMessages()
    {
        ViewData["Title"] = "Contact Messages";
        ViewData["AdminNav"] = "contact";

        var rows = await db.AdminContactMessages
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var model = new AdminContactMessagesViewModel
        {
            Items = rows.Select(x => new AdminContactMessageItemViewModel
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Subject = x.Subject,
                Message = x.Message,
                ReceivedOnText = x.CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt"),
                SortDateUtc = x.CreatedAtUtc
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContactMessage(int id)
    {
        var row = await db.AdminContactMessages.FirstOrDefaultAsync(x => x.Id == id);
        if (row is null)
        {
            TempData["AdminActionMessage"] = "Contact message not found.";
            return RedirectToAction(nameof(ContactMessages));
        }

        db.AdminContactMessages.Remove(row);
        await db.SaveChangesAsync();

        TempData["AdminActionMessage"] = "Contact message deleted.";
        return RedirectToAction(nameof(ContactMessages));
    }

    public async Task<IActionResult> ReviewsRatings()
    {
        ViewData["Title"] = "Reviews & Ratings";
        ViewData["AdminNav"] = "reviews";

        var rows = await db.AdminReviewRatings
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var model = new AdminReviewsRatingsViewModel
        {
            Items = rows.Select(x => new AdminReviewRatingItemViewModel
            {
                Id = x.Id,
                ReviewerName = x.ReviewerName,
                ReviewerInitials = GetInitials(x.ReviewerName),
                EventName = x.EventName,
                Rating = x.Rating,
                Comment = x.Comment,
                Status = x.Status,
                IsReported = x.IsReported,
                TimeAgo = ToRelative(x.CreatedAtUtc),
                SortDateUtc = x.CreatedAtUtc
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReviewRating(int? id, string reviewerName, string eventName, int rating, string comment, string status = "approved")
    {
        var name = (reviewerName ?? string.Empty).Trim();
        var eventTitle = (eventName ?? string.Empty).Trim();
        var text = (comment ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(eventTitle) || string.IsNullOrWhiteSpace(text))
        {
            TempData["AdminActionMessage"] = "Enter reviewer name, event name, and comment.";
            return RedirectToAction(nameof(ReviewsRatings));
        }

        var normalizedStatus = NormalizeStatus(status, "approved");
        var safeRating = Math.Clamp(rating, 1, 5);
        var existing = id.HasValue
            ? await db.AdminReviewRatings.FirstOrDefaultAsync(x => x.Id == id.Value)
            : null;

        if (existing is not null)
        {
            existing.ReviewerName = name;
            existing.EventName = eventTitle;
            existing.Rating = safeRating;
            existing.Comment = text;
            existing.Status = normalizedStatus;
            existing.IsReported = normalizedStatus == "reported";
        }
        else
        {
            db.AdminReviewRatings.Add(new AdminReviewRating
            {
                ReviewerName = name,
                EventName = eventTitle,
                Rating = safeRating,
                Comment = text,
                Status = normalizedStatus,
                IsReported = normalizedStatus == "reported",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        TempData["AdminActionMessage"] = existing is null ? "Review added." : "Review updated.";
        return RedirectToAction(nameof(ReviewsRatings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReviewRatingStatus(int id, string status)
    {
        var row = await db.AdminReviewRatings.FirstOrDefaultAsync(x => x.Id == id);
        if (row is null)
        {
            TempData["AdminActionMessage"] = "Review not found.";
            return RedirectToAction(nameof(ReviewsRatings));
        }

        var normalizedStatus = NormalizeStatus(status, row.Status);
        row.Status = normalizedStatus;
        row.IsReported = normalizedStatus == "reported";
        await db.SaveChangesAsync();

        TempData["AdminActionMessage"] = normalizedStatus switch
        {
            "reported" => "Review reported.",
            "approved" => "Review approved.",
            "pending" => "Review marked pending.",
            _ => "Review updated."
        };
        return RedirectToAction(nameof(ReviewsRatings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReviewRating(int id)
    {
        var row = await db.AdminReviewRatings.FirstOrDefaultAsync(x => x.Id == id);
        if (row is null)
        {
            TempData["AdminActionMessage"] = "Review not found.";
            return RedirectToAction(nameof(ReviewsRatings));
        }

        db.AdminReviewRatings.Remove(row);
        await db.SaveChangesAsync();

        TempData["AdminActionMessage"] = "Review deleted.";
        return RedirectToAction(nameof(ReviewsRatings));
    }

    private static string NormalizeUserRoleFilter(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "attend" => "attend",
            "organizer" => "organizer",
            "admin" => "admin",
            _ => "all"
        };
    }

    private static string NormalizeStatus(string value, string fallback = "pending")
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pending" => "pending",
            "approved" => "approved",
            "reported" => "reported",
            "rejected" => "rejected",
            "featured" => "featured",
            "approve" => "approved",
            "reject" => "rejected",
            _ => fallback
        };
    }

    private static string GetInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "NA";
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1)
        {
            var v = words[0];
            return v.Length >= 2 ? v[..2].ToUpperInvariant() : v.ToUpperInvariant();
        }

        return string.Concat(words[0][0], words[1][0]).ToUpperInvariant();
    }

    private static string ToRelative(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 60) return $"{Math.Max(1, (int)span.TotalMinutes)} minutes ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} days ago";
        return $"{(int)(span.TotalDays / 7)} week ago";
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

    private async Task MirrorUserToRoleDatabase(UserAccount user)
    {
        var connectionString = user.Role switch
        {
            "admin" => config.GetConnectionString("AdminConnection"),
            "organizer" => config.GetConnectionString("OrganizerConnection"),
            "attend" => config.GetConnectionString("AttendConnection"),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        connectionString = NormalizeSqliteConnectionString(connectionString);

        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();

        await using (var createCmd = conn.CreateCommand())
        {
            createCmd.CommandText =
                """
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER NOT NULL CONSTRAINT PK_Users PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    PasswordText TEXT NOT NULL DEFAULT '',
                    Role TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );
                """;
            await createCmd.ExecuteNonQueryAsync();
        }

        await using (var alterCmd = conn.CreateCommand())
        {
            alterCmd.CommandText = "ALTER TABLE Users ADD COLUMN PasswordText TEXT NOT NULL DEFAULT '';";
            try
            {
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch
            {
            }
        }

        await using (var indexCmd = conn.CreateCommand())
        {
            indexCmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Email ON Users (Email);";
            await indexCmd.ExecuteNonQueryAsync();
        }

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText =
            """
            INSERT INTO Users (FullName, Email, PasswordHash, PasswordText, Role, CreatedAtUtc)
            VALUES ($fullName, $email, $passwordHash, $passwordText, $role, $createdAtUtc)
            ON CONFLICT(Email) DO UPDATE SET
                FullName = excluded.FullName,
                PasswordHash = excluded.PasswordHash,
                PasswordText = excluded.PasswordText,
                Role = excluded.Role,
                CreatedAtUtc = excluded.CreatedAtUtc;
            """;
        insertCmd.Parameters.AddWithValue("$fullName", user.FullName);
        insertCmd.Parameters.AddWithValue("$email", user.Email);
        insertCmd.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        insertCmd.Parameters.AddWithValue("$passwordText", user.PasswordText ?? string.Empty);
        insertCmd.Parameters.AddWithValue("$role", user.Role);
        insertCmd.Parameters.AddWithValue("$createdAtUtc", user.CreatedAtUtc.ToString("O"));
        await insertCmd.ExecuteNonQueryAsync();
    }

    private string NormalizeSqliteConnectionString(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource) || System.IO.Path.IsPathRooted(dataSource))
        {
            return builder.ToString();
        }

        var absolutePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(env.ContentRootPath, dataSource));
        var directory = System.IO.Path.GetDirectoryName(absolutePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        builder.DataSource = absolutePath;
        return builder.ToString();
    }
}



