using Microsoft.AspNetCore.Mvc;
using DarkNetCore.Models;
using DarkNetCore.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace DarkNetCore.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;

    public AccountController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToLocal(returnUrl);
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ViewBag.Error = "Kullanıcı adı ve parola zorunludur.";
            return View();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user != null && user.PasswordHash == password)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

            TempData["SuccessMessage"] = "Başarıyla giriş yapıldı!";
            return RedirectToLocal(returnUrl);
        }

        ViewBag.Error = "Geçersiz kullanıcı adı veya parola.";
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Chat", "Dashboard");
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToLocal(returnUrl);
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string username, string email, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ViewBag.Error = "Tüm alanlar zorunludur.";
            return View();
        }

        if (username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.Error = "Bu kullanıcı adı sistem tarafından rezerve edilmiştir.";
            return View();
        }

        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower() || u.Email.ToLower() == email.ToLower()))
        {
            var userExists = await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
            if (userExists)
                ViewBag.Error = "Bu kullanıcı adı zaten kullanılıyor.";
            else
                ViewBag.Error = "Bu e-posta adresi ile zaten kayıt olunmuş.";
            return View();
        }

        var newUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            Email = email,
            PasswordHash = password,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Kayıt başarılı! Şimdi giriş yapabilirsiniz.";
        return RedirectToAction("Login", new { returnUrl });
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}
