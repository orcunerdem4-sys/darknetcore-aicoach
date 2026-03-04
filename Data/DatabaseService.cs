using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DarkNetCore.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace DarkNetCore.Data;

public class DatabaseService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DatabaseService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    // -----------------------------------------
    // User Operations
    // -----------------------------------------
    public string GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }

    // -----------------------------------------
    // Tasks Operations
    // -----------------------------------------
    public async Task<List<TaskItem>> GetTasksAsync()
    {
        var userId = GetCurrentUserId();
        return await _context.TaskItems.Where(t => t.UserId == userId).ToListAsync();
    }

    public async Task AddTaskAsync(TaskItem task)
    {
        task.UserId = GetCurrentUserId();

        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTaskAsync(TaskItem task)
    {
        var userId = GetCurrentUserId();
        var existing = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == task.Id && t.UserId == userId);
        if (existing != null)
        {
            existing.Title = task.Title;
            existing.Description = task.Description;
            existing.DueDate = task.DueDate;
            existing.Priority = task.Priority;
            existing.Category = task.Category;
            existing.IsCompleted = task.IsCompleted;
            
            await _context.SaveChangesAsync();
            
            if (task.IsCompleted && !existing.IsCompleted)
            {
                await UpdateStreakOnTaskCompleteAsync();
            }
        }
    }

    public async Task DeleteTaskAsync(string id)
    {
        var userId = GetCurrentUserId();
        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task != null)
        {
            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();
        }
    }

    // -----------------------------------------
    // Files / Drive Operations
    // -----------------------------------------
    public async Task<List<UploadedFile>> GetFilesAsync()
    {
        var userId = GetCurrentUserId();
        return await _context.UploadedFiles.Where(f => f.UserId == userId).ToListAsync();
    }

    public async Task AddFileAsync(UploadedFile file)
    {
        file.UserId = GetCurrentUserId();

        _context.UploadedFiles.Add(file);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateFileAsync(UploadedFile file)
    {
        var userId = GetCurrentUserId();
        var existing = await _context.UploadedFiles.FirstOrDefaultAsync(f => f.Id == file.Id && f.UserId == userId);
        if (existing != null)
        {
            existing.FileName = file.FileName;
            existing.Type = file.Type;
            existing.Url = file.Url;
            existing.AnalysisSummary = file.AnalysisSummary;
            
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteFileAsync(string id)
    {
        var userId = GetCurrentUserId();
        var file = await _context.UploadedFiles.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
        if (file != null)
        {
            // Find all descendants recursively to avoid FK constraints
            var allFilesToDelete = new List<UploadedFile> { file };
            var queue = new Queue<string>();
            queue.Enqueue(id);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var children = await _context.UploadedFiles.Where(f => f.ParentId == currentId).ToListAsync();
                foreach (var child in children)
                {
                    allFilesToDelete.Add(child);
                    queue.Enqueue(child.Id);
                }
            }

            // Reverse the list so leaf nodes (children) are deleted before their parents
            allFilesToDelete.Reverse();

            _context.UploadedFiles.RemoveRange(allFilesToDelete);
            await _context.SaveChangesAsync();
        }
    }

    // -----------------------------------------
    // Lessons Operations
    // -----------------------------------------
    public async Task<List<Lesson>> GetLessonsAsync()
    {
        return await _context.Lessons.ToListAsync();
    }

    // -----------------------------------------
    // Chat Session Operations
    // -----------------------------------------
    public async Task<List<ChatSession>> GetChatSessionsAsync()
    {
        var userId = GetCurrentUserId();
        return await _context.ChatSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<ChatSession> CreateChatSessionAsync(string title = "Yeni Sohbet")
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            StartedAt = DateTime.UtcNow,
            UserId = GetCurrentUserId()
        };
        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<List<ChatMessage>> GetChatMessagesAsync(string sessionId)
    {
        var userId = GetCurrentUserId();
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
        if (session == null) return new List<ChatMessage>();

        return await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task AddChatMessageAsync(string sessionId, string role, string content)
    {
        var userId = GetCurrentUserId();
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
        if (session == null) return; // Ignore if user doesn't own session

        var msg = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            Role = role,
            Content = content,
            SentAt = DateTime.UtcNow
        };
        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteChatSessionAsync(string sessionId)
    {
        var userId = GetCurrentUserId();
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
        
        if (session != null) 
        {
            var msgs = await _context.ChatMessages.Where(m => m.SessionId == sessionId).ToListAsync();
            _context.ChatMessages.RemoveRange(msgs);
            _context.ChatSessions.Remove(session);
            await _context.SaveChangesAsync();
        }
    }

    // -----------------------------------------
    // Feedback Notes Operations
    // -----------------------------------------
    public async Task SaveFeedbackAsync(string content)
    {
        var note = new FeedbackNote
        {
            Id = Guid.NewGuid().ToString(),
            Content = content,
            CreatedAt = DateTime.UtcNow,
            UserId = GetCurrentUserId()
        };
        _context.FeedbackNotes.Add(note);
        await _context.SaveChangesAsync();
    }

    public async Task<List<FeedbackNote>> GetFeedbacksAsync()
    {
        var userId = GetCurrentUserId();
        return await _context.FeedbackNotes
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    // -----------------------------------------
    // Admin Operations
    // -----------------------------------------
    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    }

    public async Task<List<FeedbackNote>> GetAllFeedbacksAsync()
    {
        return await _context.FeedbackNotes.OrderByDescending(n => n.CreatedAt).ToListAsync();
    }

    // -----------------------------------------
    // Streak Operations
    // -----------------------------------------
    public async Task<StreakRecord> GetOrCreateStreakAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return new StreakRecord();

        var streak = await _context.StreakRecords.FirstOrDefaultAsync(s => s.UserId == userId);
        if (streak == null)
        {
            streak = new StreakRecord
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                CurrentStreak = 0,
                LongestStreak = 0,
                LastCompletedDate = DateTime.MinValue
            };
            _context.StreakRecords.Add(streak);
            await _context.SaveChangesAsync();
        }
        return streak;
    }

    public async Task UpdateStreakOnTaskCompleteAsync()
    {
        var streak = await GetOrCreateStreakAsync();
        var today = DateTime.UtcNow.Date;

        if (streak.LastCompletedDate.Date == today)
        {
            // Already completed a task today, streak doesn't change
            return;
        }

        if (streak.LastCompletedDate.Date == today.AddDays(-1))
        {
            // Completed a task yesterday, increment streak
            streak.CurrentStreak++;
        }
        else
        {
            // Missed a day, reset streak
            streak.CurrentStreak = 1;
        }

        if (streak.CurrentStreak > streak.LongestStreak)
        {
            streak.LongestStreak = streak.CurrentStreak;
        }

        streak.LastCompletedDate = today;
        await _context.SaveChangesAsync();
    }

    // -----------------------------------------
    // Sleep Tracker Operations
    // -----------------------------------------
    public async Task<List<SleepRecord>> GetRecentSleepRecordsAsync(int days = 7)
    {
        var userId = GetCurrentUserId();
        var cutoff = DateTime.UtcNow.AddDays(-days);
        
        return await _context.SleepRecords
            .Where(s => s.UserId == userId && s.SleepEnd >= cutoff)
            .OrderBy(s => s.SleepEnd)
            .ToListAsync();
    }

    public async Task AddSleepRecordAsync(SleepRecord record)
    {
        record.Id = Guid.NewGuid().ToString();
        record.UserId = GetCurrentUserId();
        _context.SleepRecords.Add(record);
        await _context.SaveChangesAsync();
    }
}


