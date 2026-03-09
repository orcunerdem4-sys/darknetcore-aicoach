using System;

namespace DarkNetCore.Models;

public class GroupNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    public string GroupId { get; set; } = string.Empty;
    public StudyGroup? Group { get; set; }
}
