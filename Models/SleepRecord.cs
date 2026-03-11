using System.ComponentModel.DataAnnotations.Schema;

namespace DarkNetCore.Models;

public class SleepRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public User? User { get; set; }

    public DateTime SleepStart { get; set; }
    public DateTime SleepEnd { get; set; }
    public double TotalHours => (SleepEnd - SleepStart).TotalHours;

    [Column("QualityScore")]
    public int SleepTarget { get; set; } = 8; // Default goal is 8 hours
    public string Notes { get; set; } = string.Empty;
}
