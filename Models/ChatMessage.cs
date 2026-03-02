using System;

namespace DarkNetCore.Models;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string SessionId { get; set; } = string.Empty;
    public ChatSession? Session { get; set; }

    public string Role { get; set; } = "User"; // User or Assistant
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.Now;
}
