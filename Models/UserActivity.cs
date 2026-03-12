using System;
using System.ComponentModel.DataAnnotations;

namespace DarkNetCore.Models;

public class UserActivity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string PanelName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public double TotalSeconds { get; set; } = 0;
}
