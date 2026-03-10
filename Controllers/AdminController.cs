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

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AuthorizeDevice(string secret, string deviceName)
    {
        // 123456 gibi basit bir şifre yerine daha güvenli bir secret string kullanılabilir.
        if (secret == "DopamindAdmin2026")
        {
            Response.Cookies.Append("AdminDeviceAuth", "Authorized", new CookieOptions { 
                Expires = DateTimeOffset.UtcNow.AddYears(10),
                HttpOnly = true,
                IsEssential = true
            });
            return Content($"Basarili! Bu cihaz ({deviceName}) admin paneli icin yetkilendirildi.");
        }
        return Content("Yetkisiz islem.");
    }

    public async Task<IActionResult> Index()
    {
        // Yalnızca yetkilendirilmiş cihazlardan girişe izin ver
        if (Request.Cookies["AdminDeviceAuth"] != "Authorized")
        {
            return RedirectToAction("Index", "Dashboard");
        }

        var users = await _dataService.GetAllUsersAsync();
        var feedbacks = await _dataService.GetAllFeedbacksAsync();
        var errors = await _dataService.GetErrorLogsAsync();

        ViewBag.Users = users;
        ViewBag.Feedbacks = feedbacks;
        ViewBag.Errors = errors;

        return View();
    }
}
