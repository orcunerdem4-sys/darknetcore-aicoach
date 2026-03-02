using System;
using System.Collections.Generic;

namespace DarkNetCore.Models;

public class Lesson
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string ColorCode { get; set; } = "#FFFFFF";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<UploadedFile> Files { get; set; } = new List<UploadedFile>();
}
