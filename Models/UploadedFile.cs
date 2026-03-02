using System;
using System.Collections.Generic;

namespace DarkNetCore.Models;

public class UploadedFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    
    public string? LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadDate { get; set; } = DateTime.Now;
    public string? AnalysisSummary { get; set; }
    public ResourceType Type { get; set; } = ResourceType.Document;
    public string? Url { get; set; }
    
    public string? ParentId { get; set; }
    public UploadedFile? Parent { get; set; }
    public ICollection<UploadedFile> Children { get; set; } = new List<UploadedFile>();
    
    // Deep Analysis Metrics
    public int WordCount { get; set; }
    public int PageCount { get; set; }
    public int ComplexityScore { get; set; } = 1; // 1-10
    public double EstimatedStudyTime { get; set; } // in Hours
    public string? Topic { get; set; }
}

public enum ResourceType
{
    Document,
    Image,
    Link,
    Video,
    Folder
}
