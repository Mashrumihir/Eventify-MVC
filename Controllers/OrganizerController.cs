using Microsoft.AspNetCore.Mvc;

namespace Eventify.Controllers;

public class OrganizerController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Organizer";
        return View();
    }
}
