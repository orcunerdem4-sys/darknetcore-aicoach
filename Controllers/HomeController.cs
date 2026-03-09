using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DarkNetCore.Models;

namespace DarkNetCore.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var exceptionHandlerPathFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        ViewData["ExceptionMessage"] = exceptionHandlerPathFeature?.Error?.Message;
        ViewData["ExceptionStackTrace"] = exceptionHandlerPathFeature?.Error?.StackTrace;
        
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
