using Microsoft.AspNetCore.Mvc;

namespace Eventify.Controllers;

public class AdminController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Admin";
        return View();
    }
}
