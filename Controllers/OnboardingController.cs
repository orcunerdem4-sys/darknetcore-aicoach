using Microsoft.AspNetCore.Mvc;

namespace DarkNetCore.Controllers;

public class OnboardingController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Submit(string name, string role)
    {
        // For now, just redirect to Dashboard. In real app, save to DB.
        return RedirectToAction("Index", "Dashboard");
    }
}
