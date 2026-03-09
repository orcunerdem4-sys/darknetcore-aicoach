using System;

namespace DarkNetCore.Models;

public class MutedUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    // The user who pressed the 'Mute' button
    public string MuterUserId { get; set; } = string.Empty;
    public User? MuterUser { get; set; }

    // The user who is being muted
    public string MutedUserId { get; set; } = string.Empty;
    public User? MutedUserEntity { get; set; }

    // The context group
    public string GroupId { get; set; } = string.Empty;
    public StudyGroup? Group { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
