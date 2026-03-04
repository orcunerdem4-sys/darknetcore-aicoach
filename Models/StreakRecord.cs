namespace DarkNetCore.Models;

public class StreakRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    public DateTime LastCompletedDate { get; set; }
    public int CurrentStreak { get; set; } = 0;
    public int LongestStreak { get; set; } = 0;
}
