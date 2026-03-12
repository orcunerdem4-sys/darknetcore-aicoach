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
    public async Task SaveFeedbackAsync(string content, string? imagePath = null)
    {
        var note = new FeedbackNote
        {
            Id = Guid.NewGuid().ToString(),
            Content = content,
            ImagePath = imagePath,
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
    // Error Logging Operations
    // -----------------------------------------
    public async Task LogErrorAsync(string? message, string? stackTrace, string? path)
    {
        var errorLog = new ErrorLog
        {
            Id = Guid.NewGuid().ToString(),
            Message = message,
            StackTrace = stackTrace,
            Path = path,
            UserId = GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow
        };
        
        _context.ErrorLogs.Add(errorLog);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ErrorLog>> GetErrorLogsAsync()
    {
        return await _context.ErrorLogs.OrderByDescending(e => e.CreatedAt).ToListAsync();
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

    // -----------------------------------------
    // Study Group Operations
    // -----------------------------------------
    public async Task<StudyGroup?> GetUserGroupAsync()
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.Include(u => u.Group).FirstOrDefaultAsync(u => u.Id == userId);
        return user?.Group;
    }

    public async Task<StudyGroup> CreateGroupAsync(string name)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        
        var group = new StudyGroup
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            JoinCode = GenerateJoinCode()
        };
        
        _context.StudyGroups.Add(group);
        user!.GroupId = group.Id;
        
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task<bool> JoinGroupAsync(string joinCode)
    {
        var group = await _context.StudyGroups.FirstOrDefaultAsync(g => g.JoinCode == joinCode);
        if (group == null) return false;

        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.GroupId = group.Id;
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task LeaveGroupAsync()
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.GroupId = null;
            await _context.SaveChangesAsync();
        }
    }

    // Group Notes
    public async Task<List<GroupNote>> GetGroupNotesAsync(string groupId)
    {
        return await _context.GroupNotes
            .Include(n => n.User)
            .Where(n => n.GroupId == groupId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task AddGroupNoteAsync(string groupId, string content)
    {
        var note = new GroupNote
        {
            Id = Guid.NewGuid().ToString(),
            GroupId = groupId,
            UserId = GetCurrentUserId(),
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        _context.GroupNotes.Add(note);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteGroupNoteAsync(string noteId)
    {
        var userId = GetCurrentUserId();
        var note = await _context.GroupNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
        if (note != null)
        {
            _context.GroupNotes.Remove(note);
            await _context.SaveChangesAsync();
        }
    }

    // Group Chat Messages
    public async Task<List<GroupChatMessage>> GetGroupMessagesAsync(string groupId)
    {
        return await _context.GroupChatMessages
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task AddGroupMessageAsync(string groupId, string message, string senderName, string? userId = null)
    {
        var msg = new GroupChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            GroupId = groupId,
            UserId = userId,
            SenderName = senderName,
            Message = message,
            SentAt = DateTime.UtcNow
        };
        _context.GroupChatMessages.Add(msg);
        await _context.SaveChangesAsync();
    }

    private string GenerateJoinCode()
    {
        var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // ==========================================
    //         NOTIFICATIONS (PWA PUSH)
    // ==========================================
    public async Task<bool> SubscriptionExistsAsync(string userId, string endpoint)
    {
        return await _context.PushSubscriptions.AnyAsync(s => s.UserId == userId && s.Endpoint == endpoint);
    }

    public async Task AddPushSubscriptionAsync(PushSubscription subscription)
    {
        _context.PushSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();
    }

    public async Task<List<PushSubscription>> GetUserSubscriptionsAsync(string userId)
    {
        return await _context.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync();
    }

    public async Task<List<PushSubscription>> GetGroupSubscriptionsAsync(string groupId)
    {
        // First get the IDs of all users in this group
        var groupUserIds = await _context.Users
            .Where(u => u.GroupId == groupId)
            .Select(u => u.Id)
            .ToListAsync();

        if (!groupUserIds.Any())
            return new List<PushSubscription>();

        // Then get all their subscriptions
        return await _context.PushSubscriptions
            .Where(s => groupUserIds.Contains(s.UserId))
            .ToListAsync();
    }

    public async Task<bool> IsGroupMutedAsync(string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return user?.IsGroupMuted ?? false;
    }

    public async Task ToggleGroupMuteAsync(string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            user.IsGroupMuted = !user.IsGroupMuted;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<string>> GetMutedUserIdsAsync(string userId, string groupId)
    {
        return await _context.MutedUsers
            .Where(m => m.MuterUserId == userId && m.GroupId == groupId)
            .Select(m => m.MutedUserId)
            .ToListAsync();
    }

    public async Task ToggleUserMuteAsync(string muterId, string mutedId, string groupId)
    {
        var existing = await _context.MutedUsers.FirstOrDefaultAsync(m => 
            m.MuterUserId == muterId && m.MutedUserId == mutedId && m.GroupId == groupId);

        if (existing != null)
        {
            _context.MutedUsers.Remove(existing);
        }
        else
        {
            var muteEntry = new MutedUser
            {
                MuterUserId = muterId,
                MutedUserId = mutedId,
                GroupId = groupId,
                CreatedAt = DateTime.UtcNow
            };
            _context.MutedUsers.Add(muteEntry);
        }
        await _context.SaveChangesAsync();
    }

    // -----------------------------------------
    // User Activity Tracking
    // -----------------------------------------
    public async Task LogUserActivityAsync(string panelName)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return;

        var today = DateTime.UtcNow.Date;
        
        // Find existing activity for this user/panel today or create new
        var activity = await _context.UserActivities
            .FirstOrDefaultAsync(a => a.UserId == userId && a.PanelName == panelName && a.StartTime >= today);

        if (activity == null)
        {
            activity = new UserActivity
            {
                UserId = userId,
                PanelName = panelName,
                StartTime = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                TotalSeconds = 0
            };
            _context.UserActivities.Add(activity);
        }
        else
        {
            var now = DateTime.UtcNow;
            var secondsSinceLastSeen = (now - activity.LastSeenAt).TotalSeconds;

            // If last seen was less than 5 minutes ago, count it as continuous session
            if (secondsSinceLastSeen < 300) 
            {
                activity.TotalSeconds += secondsSinceLastSeen;
            }
            
            activity.LastSeenAt = now;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<UserActivity>> GetUserActivitiesAsync()
    {
        return await _context.UserActivities
            .OrderByDescending(a => a.LastSeenAt)
            .ToListAsync();
    }
}


