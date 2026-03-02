using System;
using System.Collections.Generic;

namespace DarkNetCore.Models;

public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    public string Title { get; set; } = "New Session";
    public DateTime StartedAt { get; set; } = DateTime.Now;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
