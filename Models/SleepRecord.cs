namespace DarkNetCore.Models;

public class SleepRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public User? User { get; set; }

    public DateTime SleepStart { get; set; }
    public DateTime SleepEnd { get; set; }
    public double TotalHours => (SleepEnd - SleepStart).TotalHours;

    public int QualityScore { get; set; } = 5; // 1 to 10
    public string Notes { get; set; } = string.Empty;
}
