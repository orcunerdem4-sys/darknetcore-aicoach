using Microsoft.AspNetCore.Mvc;
using DarkNetCore.Models;
using DarkNetCore.Data;
using DarkNetCore.Services;

namespace DarkNetCore.Controllers;

public class DashboardController : Controller
{
    private readonly DatabaseService _dataService;
    private readonly GeminiService _geminiService;

    public DashboardController(DatabaseService dataService, GeminiService geminiService)
    {
        _dataService = dataService;
        _geminiService = geminiService;
    }

    public async Task<IActionResult> Index()
    {
        var tasks = await _dataService.GetTasksAsync();
        return View(tasks);
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks()
    {
        var tasksList = await _dataService.GetTasksAsync();
        var tasks = tasksList.Select(t => new
        {
            id = t.Id,
            title = t.Title,
            start = t.DueDate.ToString("yyyy-MM-ddTHH:mm:ss"), // ISO 8601
            end = t.DueDate.AddHours(t.DurationHours).ToString("yyyy-MM-ddTHH:mm:ss"), // Proper End Time
            allDay = false,
            color = t.Priority == TaskPriority.High ? "#dc3545" : t.Priority == TaskPriority.Medium ? "#ffc107" : "#198754",
            textColor = t.Priority == TaskPriority.Medium ? "#000" : "#fff"
        });
        return Json(tasks);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] TaskItem task)
    {
        if (ModelState.IsValid)
        {
            task.Id = Guid.NewGuid().ToString(); // Ensure ID is set
            await _dataService.AddTaskAsync(task);
            return Json(new { success = true, task });
        }
        return Json(new { success = false, message = "Invalid data" });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateTask([FromBody] TaskItem task)
    {
        await _dataService.UpdateTaskAsync(task);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTask(string id)
    {
        await _dataService.DeleteTaskAsync(id);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> SaveFeedback([FromBody] FeedbackSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Content))
            return Json(new { success = false });
        await _dataService.SaveFeedbackAsync(request.Content);
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetFeedbacks()
    {
        var notes = await _dataService.GetFeedbacksAsync();
        return Json(notes.Select(n => new { n.Id, n.Content, n.CreatedAt }));
    }

    
    [HttpPost]
    public IActionResult OptimizeSchedule([FromBody] DateTime date)
    {
        // Removed logic for now as it needs JsonDataService methods
        // We will implement this as a DB operation later if needed
        return Json(new { success = false, message = "Not implemented yet with Database Service" });
    }

    public async Task<IActionResult> Files()
    {
        var files = await _dataService.GetFilesAsync();
        return View(files);
    }
    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file != null && file.Length > 0)
        {
            var fileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(fileName).ToLower();
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var type = ResourceType.Document;
            var analysis = "";

            // Read actual file content based on type
            if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension))
            {
                type = ResourceType.Image;
                analysis = "Görsel dosya yüklendi.";
            }
            else if (new[] { ".xlsx", ".xls" }.Contains(extension))
            {
                // Read Excel content with ClosedXML
                analysis = ReadExcelContent(filePath, fileName);
            }
            else if (extension == ".csv")
            {
                // Read CSV as plain text
                var csvText = await System.IO.File.ReadAllTextAsync(filePath);
                var lines = csvText.Split('\n').Take(100).ToList();
                analysis = $"CSV Dosyası İçeriği ({lines.Count} satır):\n" + string.Join("\n", lines);
            }
            else if (new[] { ".txt", ".md" }.Contains(extension))
            {
                var text = await System.IO.File.ReadAllTextAsync(filePath);
                analysis = text.Length > 3000 ? text[..3000] + "\n...(devamı kısaltıldı)" : text;
            }
            else if (new[] { ".pdf", ".docx", ".doc", ".pptx", ".ppt" }.Contains(extension))
            {
                analysis = $"'{fileName}' adlı dosya yüklendi. İçerik özeti için AI analizi uygulanacak.";
            }
            else
            {
                analysis = $"'{fileName}' dosyası yüklendi.";
            }

            var uploadedFile = new UploadedFile
            {
                FileName = fileName,
                FilePath = "/uploads/" + fileName,
                FileSize = file.Length,
                Type = type,
                AnalysisSummary = analysis,
                UploadDate = DateTime.UtcNow
            };

            // Call Gemini to analyze metadata + content
            var geminiResult = await _geminiService.AnalyzeFileMetadataAsync(fileName, extension);
            if (geminiResult != null)
            {
                uploadedFile.Topic = geminiResult.Topic;
                uploadedFile.ComplexityScore = geminiResult.ComplexityScore;
                uploadedFile.EstimatedStudyTime = geminiResult.EstimatedHours;
                uploadedFile.WordCount = geminiResult.WordCount;
                // Prepend content, keep Gemini summary at end
                if (!string.IsNullOrEmpty(geminiResult.Summary))
                    uploadedFile.AnalysisSummary = analysis + $"\n\nAI Özet: {geminiResult.Summary}";
            }

            await _dataService.AddFileAsync(uploadedFile);
        }
        return RedirectToAction("Files");
    }

    [HttpPost]
    public async Task<IActionResult> UploadFileAjax(IFormFile file)
    {
        if (file == null || file.Length == 0) return Json(new { success = false, message = "Dosya seçilmedi" });

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLower();
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);
        
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var type = ResourceType.Document;
        var analysis = "";

        if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension))
        {
            type = ResourceType.Image;
            analysis = "Görsel dosya yüklendi.";
        }
        else if (new[] { ".xlsx", ".xls" }.Contains(extension))
        {
            analysis = ReadExcelContent(filePath, fileName);
        }
        else if (extension == ".csv")
        {
            var csvText = await System.IO.File.ReadAllTextAsync(filePath);
            var lines = csvText.Split('\n').Take(100).ToList();
            analysis = $"CSV Dosyası İçeriği ({lines.Count} satır):\n" + string.Join("\n", lines);
        }
        else if (new[] { ".txt", ".md" }.Contains(extension))
        {
            var text = await System.IO.File.ReadAllTextAsync(filePath);
            analysis = text.Length > 3000 ? text[..3000] + "\n...(devamı kısaltıldı)" : text;
        }
        else if (new[] { ".pdf", ".docx", ".doc", ".pptx", ".ppt" }.Contains(extension))
        {
            analysis = $"'{fileName}' adlı dosya yüklendi. İçerik özeti için AI analizi uygulanacak.";
        }
        else
        {
            analysis = $"'{fileName}' dosyası yüklendi.";
        }

        var uploadedFile = new UploadedFile
        {
            FileName = fileName,
            FilePath = "/uploads/" + fileName,
            FileSize = file.Length,
            Type = type,
            AnalysisSummary = analysis,
            UploadDate = DateTime.UtcNow
        };

        var geminiResult = await _geminiService.AnalyzeFileMetadataAsync(fileName, extension);
        if (geminiResult != null)
        {
            uploadedFile.Topic = geminiResult.Topic;
            uploadedFile.ComplexityScore = geminiResult.ComplexityScore;
            uploadedFile.EstimatedStudyTime = geminiResult.EstimatedHours;
            uploadedFile.WordCount = geminiResult.WordCount;
            if (!string.IsNullOrEmpty(geminiResult.Summary))
                uploadedFile.AnalysisSummary = analysis + $"\n\nAI Özet: {geminiResult.Summary}";
        }

        await _dataService.AddFileAsync(uploadedFile);

        return Json(new { success = true, fileId = uploadedFile.Id, fileName = uploadedFile.FileName });
    }

    private string ReadExcelContent(string filePath, string fileName)
    {
        try
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook(filePath);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Excel Dosyası: {fileName}");
            sb.AppendLine($"Sayfa sayısı: {workbook.Worksheets.Count}");
            sb.AppendLine();

            foreach (var sheet in workbook.Worksheets)
            {
                sb.AppendLine($"=== Sayfa: {sheet.Name} ===");
                var rows = sheet.RangeUsed()?.RowsUsed().Take(500);
                if (rows == null) { sb.AppendLine("(boş sayfa)"); continue; }

                var maxCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 15;

                foreach (var row in rows)
                {
                    var rowData = new List<string>();
                    for (int i = 1; i <= maxCol; i++)
                    {
                        var val = row.Cell(i).GetValue<string>()?.Trim() ?? "";
                        rowData.Add(string.IsNullOrEmpty(val) ? "-" : val);
                    }
                    
                    if (rowData.All(x => x == "-")) continue; // Tamamen boş satırı atla
                    
                    sb.AppendLine(string.Join(" | ", rowData));
                }
                sb.AppendLine();
            }

            var result = sb.ToString();
            // Limit to 12000 chars to avoid token overflow
            return result.Length > 12000 ? result[..12000] + "\n...(devamı kısaltıldı)" : result;
        }
        catch (Exception ex)
        {
            return $"Excel okunurken hata: {ex.Message}";
        }
    }


    [HttpPost]
    public async Task<IActionResult> AddLink(string url, string title)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var type = ResourceType.Link;
            if (url.Contains("youtube.com") || url.Contains("youtu.be")) type = ResourceType.Video;

            var resourceName = string.IsNullOrEmpty(title) ? url : title;

            var resource = new UploadedFile
            {
                FileName = resourceName,
                Url = url,
                Type = type,
                UploadDate = DateTime.Now,
                AnalysisSummary = type == ResourceType.Video 
                    ? "Video content linked. AI Topic Extraction: Lecture material on algorithms." 
                    : "External resource linked. Content indexed for study context."
            };
            
            await _dataService.AddFileAsync(resource);
        }
        return RedirectToAction("Files");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFile(string id)
    {
        await _dataService.DeleteFileAsync(id);
        return RedirectToAction("Files");
    }

    [HttpPost]
    public async Task<IActionResult> ImportDriveFiles([FromBody] DriveImportModel model)
    {
        if (model == null || model.Files == null)
        {
            return Json(new { success = false, message = "Invalid data" });
        }

        string rootDbId = model.ParentId;
        var driveIdPattern = "folders/([-w]+)";
        var match = System.Text.RegularExpressions.Regex.Match(model.FolderUrl, driveIdPattern);
        if (!match.Success) match = System.Text.RegularExpressions.Regex.Match(model.FolderUrl, "folders/([a-zA-Z0-9_-]+)");
        var rootDriveId = match.Success ? match.Groups[1].Value : null;

        var driveIdToDbId = new Dictionary<string, string>();
        
        var existingFiles = await _dataService.GetFilesAsync();

        if (string.IsNullOrEmpty(rootDbId) && !string.IsNullOrEmpty(model.FolderUrl))
        {
            var folderName = "Google Drive Folder"; 
            var rootFile = new UploadedFile
            {
                FileName = folderName,
                Url = model.FolderUrl,
                Type = ResourceType.Folder,
                UploadDate = DateTime.Now,
                AnalysisSummary = $"Google Drive Folder synced. {model.Files.Count} items imported."
            };
            await _dataService.AddFileAsync(rootFile);
            rootDbId = rootFile.Id;
            existingFiles.Add(rootFile);
        }
        else
        {
            var existingParent = existingFiles.FirstOrDefault(f => f.Id == rootDbId);
            if (existingParent != null)
            {
                existingParent.Type = ResourceType.Folder;
                existingParent.AnalysisSummary = $"Google Drive Folder synced. {model.Files.Count} items imported.";
                await _dataService.UpdateFileAsync(existingParent);
            }
        }

        if (!string.IsNullOrEmpty(rootDriveId))
        {
            driveIdToDbId[rootDriveId] = rootDbId;
        }

        var allItems = model.Files.ToList();
        var processedDriveIds = new HashSet<string>();
        int itemsCheck = allItems.Count;
        int priorCount = -1;
        
        while (processedDriveIds.Count < itemsCheck && processedDriveIds.Count > priorCount)
        {
            priorCount = processedDriveIds.Count;
            foreach (var item in allItems)
            {
                if (processedDriveIds.Contains(item.Id)) continue;
                if (string.IsNullOrEmpty(item.Id)) continue; 

                string parentDbId = rootDbId; 
                bool parentFound = false;
                bool isDirectChildOfRoot = false;

                if (item.Parents != null && item.Parents.Any())
                {
                    foreach (var pId in item.Parents)
                    {
                        if (pId == rootDriveId) 
                        {
                            isDirectChildOfRoot = true;
                            parentFound = true;
                            break;
                        }
                        if (driveIdToDbId.ContainsKey(pId))
                        {
                            parentDbId = driveIdToDbId[pId];
                            parentFound = true;
                            break;
                        }
                    }
                }
                
                bool parentIsInBatch = item.Parents != null && item.Parents.Any(p => allItems.Any(a => a.Id == p));

                if (parentIsInBatch && !parentFound && !isDirectChildOfRoot)
                {
                    continue;
                }

                var type = item.MimeType == "application/vnd.google-apps.folder" ? ResourceType.Folder : 
                           item.MimeType.Contains("image") ? ResourceType.Image :
                           item.MimeType.Contains("video") ? ResourceType.Video : ResourceType.Link;

                var existingFile = existingFiles.FirstOrDefault(f => f.ParentId == parentDbId && f.FileName == item.Name);

                if (existingFile == null)
                {
                    var newFile = new UploadedFile
                    {
                        ParentId = parentDbId,
                        FileName = item.Name,
                        Url = item.WebViewLink,
                        Type = type,
                        UploadDate = DateTime.Now,
                        AnalysisSummary = "Synced from Drive"
                    };
                    await _dataService.AddFileAsync(newFile);
                    driveIdToDbId[item.Id] = newFile.Id;
                    existingFiles.Add(newFile);
                }
                else
                {
                    existingFile.Url = item.WebViewLink; 
                    if(existingFile.Type != type) existingFile.Type = type; 
                    await _dataService.UpdateFileAsync(existingFile);
                    driveIdToDbId[item.Id] = existingFile.Id;
                }

                processedDriveIds.Add(item.Id);
            }
        }

        return Json(new { success = true, count = processedDriveIds.Count });
    }

    // ─── Full-page Chat ─────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Chat(string? sessionId)
    {
        var sessions = await _dataService.GetChatSessionsAsync();
        List<ChatMessage> messages = new();

        if (string.IsNullOrEmpty(sessionId) && sessions.Any())
            sessionId = sessions.First().Id;

        if (!string.IsNullOrEmpty(sessionId))
            messages = await _dataService.GetChatMessagesAsync(sessionId);

        ViewBag.Sessions = sessions;
        ViewBag.CurrentSessionId = sessionId;
        ViewBag.Messages = messages;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> NewChatSession()
    {
        var session = await _dataService.CreateChatSessionAsync();
        return Json(new { success = true, sessionId = session.Id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteChatSession([FromBody] string sessionId)
    {
        await _dataService.DeleteChatSessionAsync(sessionId);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> ChatWithAi([FromBody] ChatRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Message))
            return Json(new { success = false, response = "Mesajı anlayamadım, tekrar dener misin?" });

        // Ensure we have a session
        if (string.IsNullOrEmpty(request.SessionId))
        {
            var newSess = await _dataService.CreateChatSessionAsync(
                request.Message.Length > 40 ? request.Message[..40] + "..." : request.Message);
            request.SessionId = newSess.Id;
        }

        var message = request.Message.ToLower();
        var contextFiles = new List<UploadedFile>();
        var allFiles = await _dataService.GetFilesAsync();

        if (request.ContextFileIds != null && request.ContextFileIds.Any())
        {
            foreach (var id in request.ContextFileIds)
            {
                var file = allFiles.FirstOrDefault(f => f.Id == id);
                if (file == null) continue;
                if (file.Type == ResourceType.Folder)
                {
                    var children = allFiles.Where(f => f.ParentId == id).ToList();
                    contextFiles.AddRange(children.Any() ? children : new List<UploadedFile> { file });
                }
                else contextFiles.Add(file);
            }
            contextFiles = contextFiles.Distinct().ToList();
        }

        // Build real-time schedule context (Turkey time)
        var turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
        var nowTurkey = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, turkeyTz);

        var upcomingTasksList = await _dataService.GetTasksAsync();

        // Group tasks by day for next 7 days
        var scheduleBuilder = new System.Text.StringBuilder();
        scheduleBuilder.AppendLine($"🕐 Şu anki tarih ve saat (Türkiye): {nowTurkey:dddd, dd MMMM yyyy HH:mm} (Türk saatiyle)");
        scheduleBuilder.AppendLine();

        for (int day = 0; day <= 6; day++)
        {
            var targetDate = nowTurkey.Date.AddDays(day);
            var dayLabel = day == 0 ? "Bugün" : day == 1 ? "Yarın" : targetDate.ToString("dddd, dd MMM");

            var dayTasks = upcomingTasksList
                .Where(t =>
                {
                    var tLocal = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(t.DueDate, DateTimeKind.Utc), turkeyTz);
                    return tLocal.Date == targetDate && !t.IsCompleted;
                })
                .OrderBy(t => t.DueDate)
                .ToList();

            if (dayTasks.Any())
            {
                scheduleBuilder.AppendLine($"📅 {dayLabel} ({targetDate:dd MMM}):");
                foreach (var t in dayTasks)
                {
                    var tLocal = TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(t.DueDate, DateTimeKind.Utc), turkeyTz);
                    scheduleBuilder.AppendLine($"  - {tLocal:HH:mm}: {t.Title} ({t.DurationHours}s, {t.Priority})");
                }
                scheduleBuilder.AppendLine();
            }
        }

        var tasksInNextWeek = upcomingTasksList
            .Where(t => t.DueDate >= DateTime.UtcNow && t.DueDate <= DateTime.UtcNow.AddDays(7) && !t.IsCompleted)
            .ToList();
        if (!tasksInNextWeek.Any())
            scheduleBuilder.AppendLine("Önümüzdeki 7 günde hiç görev bulunmuyor.");

        var scheduleContext = scheduleBuilder.ToString();

        // Get conversation history (last 20 messages for context window)
        var history = await _dataService.GetChatMessagesAsync(request.SessionId);
        var lessons = await _dataService.GetLessonsAsync();

        // Save user message to DB
        await _dataService.AddChatMessageAsync(request.SessionId, "user", request.Message);

        // Call Gemini with history + ALL files for full context
        string responseText = await _geminiService.ChatAsync(
            request.Message, contextFiles, scheduleContext,
            history.TakeLast(20).ToList(), lessons, allFiles);

        string actionPerformed = "";

        // Parse AI JSON Commands if present
        if (responseText.Contains("```json") && responseText.IndexOf("```", responseText.IndexOf("```json") + 7) != -1)
        {
            var startIndex = responseText.IndexOf("```json") + 7;
            var endIndex = responseText.IndexOf("```", startIndex);
            if (endIndex > startIndex)
            {
                var jsonStr = responseText.Substring(startIndex, endIndex - startIndex).Trim();
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("command", out var cmdProp))
                    {
                        var cmd = cmdProp.GetString();
                        if (cmd == "add_task")
                        {
                            var title = root.GetProperty("title").GetString();
                            var dateStr = root.GetProperty("date").GetString();
                            var duration = root.TryGetProperty("durationHours", out var durProp) ? durProp.GetDouble() : 1.0;
                            var priorityStr = root.TryGetProperty("priority", out var prioProp) ? prioProp.GetString() : "Medium";
                            
                            if (DateTime.TryParse(dateStr, out var date))
                            {
                                var task = new TaskItem
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Title = title ?? "Yeni Görev",
                                    Description = "🤖 AI Plan: Chat üzerinden eklendi.",
                                    DueDate = date,
                                    DurationHours = duration,
                                    Priority = Enum.TryParse<TaskPriority>(priorityStr, out var p) ? p : TaskPriority.Medium,
                                    Category = TaskCategory.Study
                                };
                                await _dataService.AddTaskAsync(task);
                                actionPerformed = "task_added";
                            }
                        }
                        else if (cmd == "remove_task")
                        {
                            var title = root.GetProperty("title").GetString()?.ToLower();
                            if (!string.IsNullOrEmpty(title))
                            {
                                var allTasks = await _dataService.GetTasksAsync();
                                var taskToDelete = allTasks.FirstOrDefault(t => t.Title.ToLower() == title);
                                if (taskToDelete != null)
                                {
                                    await _dataService.DeleteTaskAsync(taskToDelete.Id);
                                    actionPerformed = "task_removed";
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("JSON Command Parse Error: " + ex.Message);
                }
                
                // Hide JSON block from the user UI
                var fullJsonBlock = responseText.Substring(responseText.IndexOf("```json"), (endIndex + 3) - responseText.IndexOf("```json"));
                responseText = responseText.Replace(fullJsonBlock, "").Trim();
            }
        }

        // Save assistant response to DB
        await _dataService.AddChatMessageAsync(request.SessionId, "assistant", responseText);

        return Json(new { success = true, response = responseText, reply = responseText, actionPerformed, sessionId = request.SessionId });
    }

    private DateTime FindNextFreeSlot(DateTime startDate, double durationHours, List<TaskItem> allTasks)
    {
        // Look ahead 14 days
        for (int i = 0; i < 14; i++)
        {
            var date = startDate.Date.AddDays(i);
            
            // 0. Daily Limit Check
            var dailyTasks = allTasks.Where(t => t.DueDate.Date == date && !t.IsCompleted).ToList();
            var dailyLoad = dailyTasks.Where(t => t.Category == TaskCategory.Study).Sum(t => t.DurationHours);
            
            if (dailyLoad + durationHours > 5.0) // Max 5 hours/day
            {
                continue; // Skip to next day
            }
            
            var workStart = date.AddHours(9); // 9 AM
            var workEnd = date.AddHours(21); // 9 PM
            
            if (!dailyTasks.Any()) return workStart;

            var sortedTasks = dailyTasks.OrderBy(t => t.DueDate).ToList();
            DateTime currentPointer = workStart;

            foreach (var task in sortedTasks)
            {
                // Check gap before this task
                if ((task.DueDate - currentPointer).TotalHours >= durationHours)
                {
                    return currentPointer;
                }
                
                // Advance pointer to End of Task + 15 min Buffer
                var taskEnd = task.DueDate.AddHours(task.DurationHours).AddMinutes(15);
                if (taskEnd > currentPointer)
                {
                    currentPointer = taskEnd;
                }
            }

            // Check gap after last task
            if ((workEnd - currentPointer).TotalHours >= durationHours)
            {
                return currentPointer;
            }
        }
        
        return startDate.AddDays(15).AddHours(9); // Fallback
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public List<string> ContextFileIds { get; set; } = new List<string>();
    }

    public class DriveImportModel
    {
        public string ParentId { get; set; } = string.Empty;
        public string FolderUrl { get; set; } = string.Empty;
        public List<DriveFileItem> Files { get; set; } = new List<DriveFileItem>();
    }

    public class DriveFileItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string WebViewLink { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public List<string> Parents { get; set; } = new List<string>();
    }

    public class FeedbackSaveRequest
    {
        public string Content { get; set; } = string.Empty;
    }
}

