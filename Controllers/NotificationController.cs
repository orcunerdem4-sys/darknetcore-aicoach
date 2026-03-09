using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using DarkNetCore.Data;
using DarkNetCore.Models;

namespace DarkNetCore.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly DatabaseService _dbService;
        private readonly IConfiguration _configuration;

        public NotificationController(DatabaseService dbService, IConfiguration configuration)
        {
            _dbService = dbService;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult GetVapidPublicKey()
        {
            var publicKey = _configuration["VapidDetails:PublicKey"];
            return Json(new { publicKey });
        }

        [HttpPost]
        public async Task<IActionResult> Subscribe([FromBody] PushSubscription subscription)
        {
            if (subscription == null)
            {
                return BadRequest("Invalid subscription data.");
            }

            var userId = _dbService.GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            subscription.UserId = userId;
            
            // Check if this exact endpoint is already subscribed for this user to avoid duplicates
            var exists = await _dbService.SubscriptionExistsAsync(userId, subscription.Endpoint);
            if (!exists)
            {
                await _dbService.AddPushSubscriptionAsync(subscription);
            }

            return Ok(new { success = true });
        }
    }
}
