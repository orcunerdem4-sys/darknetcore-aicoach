using System.Text.Json;
using DarkNetCore.Models;

namespace DarkNetCore.Services;

public class JsonDataService
{
    private readonly string _dataFilePath;
    private DataContainer _data;
    private readonly IServiceProvider _serviceProvider;

    public JsonDataService(IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        _dataFilePath = Path.Combine(env.ContentRootPath, "app_data.json");
        _serviceProvider = serviceProvider;
        _data = LoadData();
    }

    private DataContainer LoadData()
    {
        if (!File.Exists(_dataFilePath))
        {
            return new DataContainer();
        }

        var json = File.ReadAllText(_dataFilePath);
        return JsonSerializer.Deserialize<DataContainer>(json) ?? new DataContainer();
    }

    public void SaveChanges()
    {
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_dataFilePath, json);
    }

    // Task Methods
    public List<TaskItem> GetTasks() => _data.Tasks;

    public void AddTask(TaskItem task)
    {
        _data.Tasks.Add(task);
        SaveChanges();
    }

    public void UpdateTask(TaskItem task)
    {
        var existing = _data.Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (existing != null)
        {
            existing.Title = task.Title;
            existing.Description = task.Description;
            existing.DueDate = task.DueDate;
            existing.Priority = task.Priority;
            existing.Category = task.Category;
            existing.IsCompleted = task.IsCompleted;
            SaveChanges();
        }
    }

    public void DeleteTask(string id)
    {
        var task = _data.Tasks.FirstOrDefault(t => t.Id == id);
        if (task != null)
        {
            _data.Tasks.Remove(task);
            SaveChanges();
        }
    }

    // File Methods
    public List<UploadedFile> GetFiles() => _data.Files;

    public void AddFile(UploadedFile file)
    {
        PerformDeepAnalysis(file);
        _data.Files.Add(file);
        SaveChanges();
    }

    private void PerformDeepAnalysis(UploadedFile file)
    {
        // Use a scope to get GeminiService (it's scoped, this is singleton)
        using (var scope = _serviceProvider.CreateScope())
        {
            var gemini = scope.ServiceProvider.GetRequiredService<GeminiService>();
            
            // Run sync for now as this is called from non-async context (AddFile)
            // ideally refactor AddFile to be async, but for speed:
            var task = gemini.AnalyzeFileMetadataAsync(file.FileName, file.Type.ToString());
            task.Wait();
            var result = task.Result;

            if (result != null)
            {
                file.Topic = result.Topic;
                file.ComplexityScore = result.ComplexityScore;
                file.WordCount = result.WordCount;
                file.EstimatedStudyTime = result.EstimatedHours;
                file.AnalysisSummary = $"✨ AI Analysis: {result.Summary} ({result.WordCount} words)";
                return;
            }
        }

        // Fallback to Simulation if API fails
        var name = file.FileName.ToLower();
        file.ComplexityScore = 5; 
        file.Topic = "General Study";

        if (name.Contains("intro") || name.Contains("basic")) { file.ComplexityScore = 3; file.Topic = "Fundamentals"; }
        else if (name.Contains("advanced") || name.Contains("complex")) { file.ComplexityScore = 8; file.Topic = "Advanced Concepts"; }

        var rand = new Random(file.Id.GetHashCode());
        file.WordCount = file.Type == ResourceType.Folder ? rand.Next(5000, 20000) : rand.Next(1500, 15000);
        
        file.EstimatedStudyTime = 1.5;
        file.AnalysisSummary = "Simulated Analysis (API Unavailable)";
    }

    public void UpdateFile(UploadedFile file)
    {
        var existing = _data.Files.FirstOrDefault(f => f.Id == file.Id);
        if (existing != null)
        {
            existing.FileName = file.FileName;
            existing.AnalysisSummary = file.AnalysisSummary;
            existing.Url = file.Url;
            existing.Type = file.Type;
            
            // Recalculate metrics if missing (e.g. synced folders that were skipped)
            if (existing.WordCount == 0 || existing.ComplexityScore <= 1)
            {
                PerformDeepAnalysis(existing);
            }
            
            SaveChanges();
        }
    }

    public void DeleteFile(string id)
    {
        var file = _data.Files.FirstOrDefault(f => f.Id == id);
        if (file != null)
        {
            _data.Files.Remove(file);
            
            // Delete children if folder
            var children = _data.Files.Where(f => f.ParentId == id).ToList();
            foreach (var child in children)
            {
                _data.Files.Remove(child);
            }
            
            SaveChanges();
        }
    }
    
    // AI Mock Logic: Basic "Busy Day" Optimization
    public List<TaskItem> OptimizeSchedule(DateTime date)
    {
        var tasksOnDate = _data.Tasks.Where(t => t.DueDate.Date == date.Date && !t.IsCompleted).ToList();
        
        // Simple logic: If more than 3 high priority tasks, suggest moving low priority ones
        if (tasksOnDate.Count > 5)
        {
            // Find low priority tasks to reschedule
            var lowPriorityTasks = tasksOnDate.Where(t => t.Priority == TaskPriority.Low).ToList();
            foreach (var task in lowPriorityTasks)
            {
                task.DueDate = task.DueDate.AddDays(1); // Move to next day
            }
            SaveChanges();
            return lowPriorityTasks; // Return changed tasks
        }

        return new List<TaskItem>();
    }

    private class DataContainer
    {
        public List<TaskItem> Tasks { get; set; } = new();
        public List<UploadedFile> Files { get; set; } = new();
    }
}
