using Eventify.Data;
using Eventify.Models;
using Eventify.Utilities;
using Eventify.ViewModels.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eventify.Controllers;

public class AuthController(EventifyDbContext db, IConfiguration config) : Controller
{
    private const string VerifyEmailPurpose = "verify-email";
    private const string ResetPasswordPurpose = "reset-password";

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["AuthMessage"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Login()
    {
        ViewData["Title"] = "Login";
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["Title"] = "Login";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var hashedPassword = PasswordHasher.Hash(model.Password);

        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == normalizedEmail &&
            u.PasswordHash == hashedPassword &&
            u.Role == model.Role);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        if (!user.IsEmailVerified)
        {
            await GenerateAuthCodeAsync(user.Email, VerifyEmailPurpose);
            TempData["AuthMessage"] = "Please verify your email before signing in.";
            return RedirectToAction(nameof(VerifyEmail), new { email = user.Email, purpose = VerifyEmailPurpose });
        }

        HttpContext.Session.SetString("UserEmail", user.Email);
        HttpContext.Session.SetString("UserFullName", user.FullName);
        HttpContext.Session.SetString("UserRole", user.Role);

        TempData["AuthMessage"] = $"Welcome, {user.FullName}.";
        return RedirectToRolePage(model.Role);
    }

    public IActionResult Register()
    {
        ViewData["Title"] = "Register";
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        ViewData["Title"] = "Register";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Email), "Email already exists.");
            return View(model);
        }

        var user = new UserAccount
        {
            FullName = model.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.Hash(model.Password),
            PasswordText = model.Password,
            Role = model.Role,
            PasswordChangedAtUtc = DateTime.UtcNow,
            IsEmailVerified = true,
            EmailVerifiedAtUtc = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        await EnsureProfileExistsForRoleAsync(user);
        await RoleDatabaseMirror.MirrorUserAsync(config, user);

        TempData["AuthMessage"] = "Registration successful. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult ForgotPassword()
    {
        ViewData["Title"] = "Forgot Password";
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        ViewData["Title"] = "Forgot Password";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.Email), "We couldn't find an account for that email.");
            return View(model);
        }

        await GenerateAuthCodeAsync(user.Email, ResetPasswordPurpose);
        TempData["AuthMessage"] = "Reset code generated. Enter it below to continue.";
        return RedirectToAction(nameof(VerifyEmail), new { email = user.Email, purpose = ResetPasswordPurpose });
    }

    public IActionResult VerifyEmail(string email, string purpose = VerifyEmailPurpose)
    {
        ViewData["Title"] = purpose == ResetPasswordPurpose ? "Verify Reset Code" : "Verify Email";
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var resolvedPurpose = string.IsNullOrWhiteSpace(purpose) ? VerifyEmailPurpose : purpose;

        return View(new VerifyEmailViewModel
        {
            Email = normalizedEmail,
            Purpose = resolvedPurpose
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        ViewData["Title"] = model.Purpose == ResetPasswordPurpose ? "Verify Reset Code" : "Verify Email";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var code = NormalizeCode(model.Code);
        var authCode = await db.AuthCodes
            .Where(x => x.Email.ToLower() == normalizedEmail && x.Purpose == model.Purpose && !x.IsUsed)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (authCode is null || authCode.ExpiresAtUtc < DateTime.UtcNow || authCode.Code != code)
        {
            ModelState.AddModelError(nameof(model.Code), "Invalid or expired code.");
            return View(model);
        }

        authCode.IsUsed = true;
        authCode.UsedAtUtc = DateTime.UtcNow;

        if (model.Purpose == VerifyEmailPurpose)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (user is not null)
            {
                user.IsEmailVerified = true;
                user.EmailVerifiedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return RedirectToAction(nameof(EmailVerified));
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(ResetPassword), new { email = normalizedEmail });
    }

    public IActionResult EmailVerified()
    {
        ViewData["Title"] = "Email Verified";
        return View();
    }

    public IActionResult ResetPassword(string email)
    {
        ViewData["Title"] = "Reset Password";
        return View(new ResetPasswordViewModel { Email = email ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ViewData["Title"] = "Reset Password";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Account not found.");
            return View(model);
        }

        user.PasswordHash = PasswordHasher.Hash(model.Password);
        user.PasswordText = model.Password;
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await RoleDatabaseMirror.MirrorUserAsync(config, user);

        TempData["AuthMessage"] = "Password reset successful. You can sign in now.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendCode(string email, string purpose)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            await GenerateAuthCodeAsync(email.Trim().ToLowerInvariant(), purpose);
        }

        TempData["AuthMessage"] = "A new verification code has been generated.";
        return RedirectToAction(nameof(VerifyEmail), new { email, purpose });
    }

    private IActionResult RedirectToRolePage(string role)
    {
        return role switch
        {
            "organizer" => RedirectToAction("Index", "Organizer"),
            "admin" => RedirectToAction("Index", "Admin"),
            _ => RedirectToAction("Dashboard", "Attend")
        };
    }

    private async Task<string> GenerateAuthCodeAsync(string email, string purpose)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var activeCodes = await db.AuthCodes
            .Where(x => x.Email.ToLower() == normalizedEmail && x.Purpose == purpose && !x.IsUsed)
            .ToListAsync();

        foreach (var activeCode in activeCodes)
        {
            activeCode.IsUsed = true;
            activeCode.UsedAtUtc = DateTime.UtcNow;
        }

        var code = Random.Shared.Next(100000, 999999).ToString();

        db.AuthCodes.Add(new AuthCode
        {
            Email = normalizedEmail,
            Purpose = purpose,
            Code = code,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        });

        await db.SaveChangesAsync();
        return code;
    }

    private static string NormalizeCode(string code)
    {
        return new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private async Task EnsureProfileExistsForRoleAsync(UserAccount user)
    {
        if (string.Equals(user.Role, "attend", StringComparison.OrdinalIgnoreCase))
        {
            var existingAttendProfile = await db.AttendProfileSettings
                .FirstOrDefaultAsync(x => x.UserEmail == user.Email);

            if (existingAttendProfile is null)
            {
                db.AttendProfileSettings.Add(new AttendProfileSetting
                {
                    UserEmail = user.Email
                });
                await db.SaveChangesAsync();
            }

            return;
        }

        if (string.Equals(user.Role, "organizer", StringComparison.OrdinalIgnoreCase))
        {
            var existingOrganizerProfile = await db.OrganizerProfileSettings
                .FirstOrDefaultAsync(x => x.UserEmail == user.Email);

            if (existingOrganizerProfile is null)
            {
                db.OrganizerProfileSettings.Add(new OrganizerProfileSetting
                {
                    UserEmail = user.Email
                });
                await db.SaveChangesAsync();
            }
        }
    }

}

