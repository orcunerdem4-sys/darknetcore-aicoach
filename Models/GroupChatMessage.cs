using System;

namespace DarkNetCore.Models;

public class GroupChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GroupId { get; set; } = string.Empty;
    public StudyGroup? Group { get; set; }

    // User can be null if it's the AI coach responding
    public string? UserId { get; set; }
    public User? User { get; set; }

    public string SenderName { get; set; } = string.Empty; // "User A", "AI Coach"
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
