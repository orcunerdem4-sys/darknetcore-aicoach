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
            return Content($"<div style='background:#121212;color:#00ff00;height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:center;font-family:sans-serif;'>" +
                           $"<h2>✅ Başarılı!</h2>" +
                           $"<p>Bu cihaz ({deviceName}) admin paneli için yetkilendirildi.</p>" +
                           $"<a href='/Admin' style='color:#fff;text-decoration:none;padding:10px 20px;border:1px solid #fff;border-radius:5px;'>Panele Git</a>" +
                           $"<script>setTimeout(() => window.location.href='/Admin', 2000);</script></div>", "text/html");
        }
        return Content("Yetkisiz işlem.");
    }

    public async Task<IActionResult> Index()
    {
        // 1. Kullanıcı kimliği kontrolü (Sadece senin hesabın)
        if (User.Identity?.Name != "Beytullah")
        {
            return RedirectToAction("Index", "Dashboard");
        }

        // 2. Cihaz yetki kontrolü
        if (Request.Cookies["AdminDeviceAuth"] != "Authorized")
        {
            return RedirectToAction("Index", "Dashboard");
        }

        var users = await _dataService.GetAllUsersAsync();
        var feedbacks = await _dataService.GetAllFeedbacksAsync();
        var errors = await _dataService.GetErrorLogsAsync();
        var activities = await _dataService.GetUserActivitiesAsync();

        ViewBag.Users = users;
        ViewBag.Feedbacks = feedbacks;
        ViewBag.Errors = errors;
        ViewBag.Activities = activities;

        return View();
    }
}
