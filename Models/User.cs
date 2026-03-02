using System;
using System.Collections.Generic;

namespace DarkNetCore.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<UploadedFile> Files { get; set; } = new List<UploadedFile>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}
