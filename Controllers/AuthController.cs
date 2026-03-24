using Eventify.Data;
using Eventify.Models;
using Eventify.Utilities;
using Eventify.ViewModels.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eventify.Controllers;

public class AuthController(EventifyDbContext db, IConfiguration config) : Controller
{
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
            Role = model.Role
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        await RoleDatabaseMirror.MirrorUserAsync(config, user);

        TempData["AuthMessage"] = "Registration successful.";
        return RedirectToAction(nameof(Login));
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
}

