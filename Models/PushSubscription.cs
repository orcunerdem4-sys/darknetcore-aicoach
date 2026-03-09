using System;

namespace DarkNetCore.Models;

public class PushSubscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    public string Endpoint { get; set; } = string.Empty;
    public string P256DH { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
