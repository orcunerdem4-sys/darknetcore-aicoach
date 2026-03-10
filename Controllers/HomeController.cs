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
        // Redirect directly to Dashboard Chat (main functional area)
        return RedirectToAction("Chat", "Dashboard");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Error([FromServices] DarkNetCore.Data.DatabaseService dataService)
    {
        var exceptionHandlerPathFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        
        if (exceptionHandlerPathFeature?.Error != null)
        {
            await dataService.LogErrorAsync(
                exceptionHandlerPathFeature.Error.Message,
                exceptionHandlerPathFeature.Error.StackTrace,
                exceptionHandlerPathFeature.Path
            );
        }

        ViewData["ExceptionMessage"] = exceptionHandlerPathFeature?.Error?.Message;
        ViewData["ExceptionStackTrace"] = exceptionHandlerPathFeature?.Error?.StackTrace;
        
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
