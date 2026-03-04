using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DarkNetCore.Data;

namespace DarkNetCore.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly DatabaseService _dataService;

    public AdminController(DatabaseService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.Name != "admin" && User.Identity?.Name != "beytullah")
        {
            return RedirectToAction("Index", "Dashboard");
        }

        var users = await _dataService.GetAllUsersAsync();
        var feedbacks = await _dataService.GetAllFeedbacksAsync();

        ViewBag.Users = users;
        ViewBag.Feedbacks = feedbacks;

        return View();
    }
}
