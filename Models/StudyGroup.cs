using System;
using System.Collections.Generic;

namespace DarkNetCore.Models;

public class StudyGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Members { get; set; } = new List<User>();
    public ICollection<GroupNote> Notes { get; set; } = new List<GroupNote>();
    public ICollection<GroupChatMessage> ChatMessages { get; set; } = new List<GroupChatMessage>();
}
