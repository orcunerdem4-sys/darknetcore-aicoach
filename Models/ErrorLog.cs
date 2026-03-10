using System;
using System.ComponentModel.DataAnnotations;

namespace DarkNetCore.Models;

public class ErrorLog
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string? Message { get; set; }
    public string? StackTrace { get; set; }
    public string? Path { get; set; }
    public string? UserId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
