using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DarkNetCore.Models;
using DarkNetCore.Data;
using DarkNetCore.Services;

namespace DarkNetCore.Controllers;

[Authorize]
public class GroupController : Controller
{
    private readonly DatabaseService _dataService;
    private readonly GeminiService _geminiService;

    public GroupController(DatabaseService dataService, GeminiService geminiService)
    {
        _dataService = dataService;
        _geminiService = geminiService;
    }

    public async Task<IActionResult> Index()
    {
        var group = await _dataService.GetUserGroupAsync();
        
        // If user is not in a group, show the Join/Create page.
        if (group == null)
        {
            return View("NoGroup");
        }

        ViewBag.Notes = await _dataService.GetGroupNotesAsync(group.Id);
        ViewBag.Messages = await _dataService.GetGroupMessagesAsync(group.Id);
        
        return View(group);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RedirectToAction("Index");
        }

        await _dataService.CreateGroupAsync(name);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> JoinGroup(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return RedirectToAction("Index");
        }

        var success = await _dataService.JoinGroupAsync(joinCode.ToUpper());
        if (!success)
        {
            // Ideally add an error message to TempData
            TempData["Error"] = "Geçersiz grup kodu.";
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> LeaveGroup()
    {
        await _dataService.LeaveGroupAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> AddNote([FromBody] string content)
    {
        var group = await _dataService.GetUserGroupAsync();
        if (group == null || string.IsNullOrWhiteSpace(content))
        {
            return Json(new { success = false });
        }

        await _dataService.AddGroupNoteAsync(group.Id, content);
        
        // Fetch the created note to return to UI
        var allNotes = await _dataService.GetGroupNotesAsync(group.Id);
        var latestNote = allNotes.FirstOrDefault();
        
        return Json(new { 
            success = true, 
            id = latestNote?.Id, 
            author = latestNote?.User?.Username,
            content = latestNote?.Content,
            date = latestNote?.CreatedAt.ToString("g")
        });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteNote(string id)
    {
        await _dataService.DeleteGroupNoteAsync(id);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] GroupChatRequest request)
    {
        var group = await _dataService.GetUserGroupAsync();
        if (group == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return Json(new { success = false, message = "Geçersiz istek." });
        }

        var userId = _dataService.GetCurrentUserId();
        // Since we don't fetch the user directly here, we'll try to find the User's name from claims or just say "Sen" for now. 
        // We can get username by injecting DbContext or storing claim. 
        // We'll rely on the user finding their own name, or just use "User" for now.
        // Quick workaround: fetch current user to get username.
        
        // First save user's message
        // This is a simplified approach, ideally we should inject Context to get User name from claims
        string senderName = User.Identity?.Name ?? "Katılımcı"; 
        await _dataService.AddGroupMessageAsync(group.Id, request.Message, senderName, userId);

        // Fetch Group Notes for Context
        var notes = await _dataService.GetGroupNotesAsync(group.Id);
        var notesContext = string.Join("\n", notes.Select(n => $"{n.User?.Username ?? "Bir üye"}: {n.Content}"));

        // Fetch last 15 messages for Context
        var messages = await _dataService.GetGroupMessagesAsync(group.Id);
        var chatHistory = messages.TakeLast(15).Select(m => new ChatMessage { Role = m.SenderName == "AI Coach" ? "assistant" : "user", Content = $"[{m.SenderName}]: {m.Message}" }).ToList();

        // System prompt instructs AI to act as a coach addressing the group.
        string promptPrefix = $"Sen bu çalışma grubunun motive edici, mentor ve koç yapay zekasısın. Gruptaki ortak notlar şunlar:\n{notesContext}\n\nYeni mesaj [{senderName}] tarafından gönderildi: {request.Message}\n\nDoğal, samimi, bir WhatsApp grubundaki koç gibi cevap ver. Sadece [{senderName}] kişisine değil, bazen grubun geneline de hitap et. Asla kendini bir AI asistanı olarak tanıtma, grubun bir parçası/lideri gibi davran.";

        // Use GeminiService to get a response
        // We pass the customized prompt, no files needed for now, and no strict schedule needed for group chat unless requested
        string aiResponse = await _geminiService.ChatAsync(promptPrefix, new List<UploadedFile>(), "", chatHistory, new List<Lesson>(), new List<UploadedFile>());

        // Save AI Response
        await _dataService.AddGroupMessageAsync(group.Id, aiResponse, "AI Coach", null);

        // Send Push Notifications to Group Members
        await SendPushNotificationToGroupAsync(group, request.Message, senderName, userId);
        await SendPushNotificationToGroupAsync(group, aiResponse, "AI Coach", null);

        return Json(new { 
            success = true, 
             userMessage = new { sender = senderName, text = request.Message, time = DateTime.UtcNow.ToString("g") },
            aiMessage = new { sender = "AI Coach", text = aiResponse, time = DateTime.UtcNow.ToString("g") }
        });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleGroupMute()
    {
        var userId = _dataService.GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Json(new { success = false });

        await _dataService.ToggleGroupMuteAsync(userId);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUserMute(string mutedUserId)
    {
        var muterId = _dataService.GetCurrentUserId();
        var group = await _dataService.GetUserGroupAsync();
        
        if (string.IsNullOrEmpty(muterId) || group == null || string.IsNullOrEmpty(mutedUserId)) 
            return Json(new { success = false });

        await _dataService.ToggleUserMuteAsync(muterId, mutedUserId, group.Id);
        return Json(new { success = true });
    }

    private async Task SendPushNotificationToGroupAsync(StudyGroup group, string message, string senderName, string? senderUserId)
    {
        var currentUserId = _dataService.GetCurrentUserId();

        var config = HttpContext.RequestServices.GetService<IConfiguration>();
        var subject = config?["VapidDetails:Subject"];
        var publicKey = config?["VapidDetails:PublicKey"];
        var privateKey = config?["VapidDetails:PrivateKey"];

        if(string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
            return;

        var vapidDetails = new WebPush.VapidDetails(subject, publicKey, privateKey);
        var webPushClient = new WebPush.WebPushClient();

        var subscriptions = await _dataService.GetGroupSubscriptionsAsync(group.Id);

        // We need to check who has the group muted globally, or specifically muted this sender.
        var validSubscriptions = new List<WebPush.PushSubscription>();

        foreach (var sub in subscriptions)
        {
            // Don't send notification to the person who sent the message
            if (senderName != "AI Coach" && !string.IsNullOrEmpty(currentUserId) && sub.UserId == currentUserId)
            {
                continue;
            }

            // Check if this user has muted the whole group
            bool isGroupMuted = await _dataService.IsGroupMutedAsync(sub.UserId);
            if (isGroupMuted) continue;

            // Check if this user has muted this specific sender
            if (!string.IsNullOrEmpty(senderUserId))
            {
                var mutedUsers = await _dataService.GetMutedUserIdsAsync(sub.UserId, group.Id);
                if (mutedUsers.Contains(senderUserId))
                {
                    continue; // They muted the sender
                }
            }

            validSubscriptions.Add(new WebPush.PushSubscription(sub.Endpoint, sub.P256DH, sub.Auth));
        }

        if (!validSubscriptions.Any()) return;

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = $"Yeni Mesaj: {group.Name}",
            message = $"{senderName}: {message}",
            url = "/Group"
        });

        foreach (var pushSub in validSubscriptions)
        {
            try
            {
                await webPushClient.SendNotificationAsync(pushSub, payload, vapidDetails);
            }
            catch (WebPush.WebPushException ex)
            {
                Console.WriteLine($"Push failed for {pushSub.Endpoint}: {ex.Message}");
                // If the subscription is no longer valid (e.g., HTTP 410 Gone), we could delete it from DB here
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General push error: {ex.Message}");
            }
        }
    }

    public class GroupChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
