using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DarkNetCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkNetCore.Data;

public static class DatabaseSeeder
{
    public static void SeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        try
        {
            context.Database.Migrate(); // Ensure database runs migrations
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Migration failed: {ex.Message}");
            Console.WriteLine("[INFO] Tables may need to be created manually via Supabase SQL Editor.");
        }

        try
        {
            if (!context.Users.Any())
            {
                var defaultUser = new User
                {
                    Username = "Admin",
                    Email = "admin@darknetcore.localhost",
                    PasswordHash = "hashed_password_placeholder",
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(defaultUser);
                context.SaveChanges();

                var dataFilePath = Path.Combine(env.ContentRootPath, "app_data.json");
                if (File.Exists(dataFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(dataFilePath);
                        var dataContainer = JsonSerializer.Deserialize<DataContainer>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (dataContainer != null)
                        {
                            if (dataContainer.Tasks != null && dataContainer.Tasks.Any())
                            {
                                foreach (var task in dataContainer.Tasks)
                                {
                                    task.UserId = defaultUser.Id;
                                    context.TaskItems.Add(task);
                                }
                            }

                            if (dataContainer.Files != null && dataContainer.Files.Any())
                            {
                                foreach (var file in dataContainer.Files)
                                {
                                    file.UserId = defaultUser.Id;
                                    context.UploadedFiles.Add(file);
                                }
                            }

                            context.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error migrating data from app_data.json: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Seeding failed (tables may not exist yet): {ex.Message}");
            Console.WriteLine("[INFO] Please run the migration SQL in Supabase SQL Editor.");
        }
    }

    private class DataContainer
    {
        public System.Collections.Generic.List<TaskItem> Tasks { get; set; } = new();
        public System.Collections.Generic.List<UploadedFile> Files { get; set; } = new();
    }
}
