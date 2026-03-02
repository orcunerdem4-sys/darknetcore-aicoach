using System;

namespace DarkNetCore.Models;

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    
    public string? LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskCategory Category { get; set; } = TaskCategory.Work;
    public bool IsCompleted { get; set; } = false;
    public double DurationHours { get; set; } = 1.0;
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public enum TaskCategory
{
    Work,
    Study,
    Social,
    Personal,
    Other
}
