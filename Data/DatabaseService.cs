using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DarkNetCore.Models;
using Microsoft.EntityFrameworkCore;

namespace DarkNetCore.Data;

public class DatabaseService
{
    private readonly ApplicationDbContext _context;

    public DatabaseService(ApplicationDbContext context)
    {
        _context = context;
    }

    // -----------------------------------------
    // User Operations
    // -----------------------------------------
    public async Task<User?> GetDefaultUserAsync()
    {
        return await _context.Users.FirstOrDefaultAsync();
    }

    // -----------------------------------------
    // Tasks Operations
    // -----------------------------------------
    public async Task<List<TaskItem>> GetTasksAsync()
    {
        return await _context.TaskItems.ToListAsync();
    }

    public async Task AddTaskAsync(TaskItem task)
    {
        var user = await GetDefaultUserAsync();
        if (user != null) task.UserId = user.Id;

        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTaskAsync(TaskItem task)
    {
        var existing = await _context.TaskItems.FindAsync(task.Id);
        if (existing != null)
        {
            existing.Title = task.Title;
            existing.Description = task.Description;
            existing.DueDate = task.DueDate;
            existing.Priority = task.Priority;
            existing.Category = task.Category;
            existing.IsCompleted = task.IsCompleted;
            
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteTaskAsync(string id)
    {
        var task = await _context.TaskItems.FindAsync(id);
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
        return await _context.UploadedFiles.ToListAsync();
    }

    public async Task AddFileAsync(UploadedFile file)
    {
        var user = await GetDefaultUserAsync();
        if (user != null) file.UserId = user.Id;

        _context.UploadedFiles.Add(file);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateFileAsync(UploadedFile file)
    {
        var existing = await _context.UploadedFiles.FindAsync(file.Id);
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
        var file = await _context.UploadedFiles.FindAsync(id);
        if (file != null)
        {
            // Cascade delete children manually since RESTRICT was used to prevent cycles
            var children = await _context.UploadedFiles.Where(f => f.ParentId == id).ToListAsync();
            _context.UploadedFiles.RemoveRange(children);

            _context.UploadedFiles.Remove(file);
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
        return await _context.ChatSessions
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<ChatSession> CreateChatSessionAsync(string title = "Yeni Sohbet")
    {
        var user = await GetDefaultUserAsync();
        var session = new ChatSession
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            StartedAt = DateTime.UtcNow,
            UserId = user?.Id ?? string.Empty
        };
        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<List<ChatMessage>> GetChatMessagesAsync(string sessionId)
    {
        return await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task AddChatMessageAsync(string sessionId, string role, string content)
    {
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
        var msgs = await _context.ChatMessages.Where(m => m.SessionId == sessionId).ToListAsync();
        _context.ChatMessages.RemoveRange(msgs);
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session != null) _context.ChatSessions.Remove(session);
        await _context.SaveChangesAsync();
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
            CreatedAt = DateTime.UtcNow
        };
        _context.FeedbackNotes.Add(note);
        await _context.SaveChangesAsync();
    }

    public async Task<List<FeedbackNote>> GetFeedbacksAsync()
    {
        return await _context.FeedbackNotes
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
}


