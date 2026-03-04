using DarkNetCore.Models;
using Microsoft.EntityFrameworkCore;

namespace DarkNetCore.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<TaskItem> TaskItems { get; set; }
    public DbSet<UploadedFile> UploadedFiles { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<FeedbackNote> FeedbackNotes { get; set; }
    public DbSet<SleepRecord> SleepRecords { get; set; }
    public DbSet<StreakRecord> StreakRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UploadedFile Self-Referencing Relationship
        modelBuilder.Entity<UploadedFile>()
            .HasOne(f => f.Parent)
            .WithMany(f => f.Children)
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // User Relationships (Cascade Delete)
        modelBuilder.Entity<User>()
            .HasMany(u => u.Lessons)
            .WithOne(l => l.User)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Tasks)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Files)
            .WithOne(f => f.User)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ChatSessions)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lesson Relationships Setup (Set Null on delete)
        modelBuilder.Entity<Lesson>()
            .HasMany(l => l.Tasks)
            .WithOne(t => t.Lesson)
            .HasForeignKey(t => t.LessonId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Lesson>()
            .HasMany(l => l.Files)
            .WithOne(f => f.Lesson)
            .HasForeignKey(f => f.LessonId)
            .OnDelete(DeleteBehavior.SetNull);

        // ChatSession - ChatMessage
        modelBuilder.Entity<ChatSession>()
            .HasMany(s => s.Messages)
            .WithOne(m => m.Session)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
