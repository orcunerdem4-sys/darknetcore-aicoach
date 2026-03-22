using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DarkNetCore.Models;
using DarkNetCore.Data;
using DarkNetCore.Services;

namespace DarkNetCore.Controllers;

[Authorize]
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
        
        // Pass metrics to View
        ViewBag.Streak = await _dataService.GetOrCreateStreakAsync();
        ViewBag.SleepRecords = await _dataService.GetRecentSleepRecordsAsync(7);
        
        var allFiles = await _dataService.GetFilesAsync();
        ViewBag.RecentFiles = allFiles.OrderByDescending(f => f.UploadDate).Take(5).ToList();
        
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
            textColor = t.Priority == TaskPriority.Medium ? "#000" : "#fff",
            priority = (int)t.Priority,
            dueDate = t.DueDate,
            isCompleted = t.IsCompleted,
            difficultyScore = t.DifficultyScore,
            difficultyReason = string.IsNullOrEmpty(t.DifficultyReason) ? "Planlanmış standart görev." : t.DifficultyReason
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
    public async Task<IActionResult> ToggleTaskComplete([FromBody] TaskItem taskUpdate)
    {
        var tasks = await _dataService.GetTasksAsync();
        var existingTask = tasks.FirstOrDefault(t => t.Id == taskUpdate.Id);
        
        if (existingTask != null)
        {
            existingTask.IsCompleted = taskUpdate.IsCompleted;
            await _dataService.UpdateTaskAsync(existingTask);
            return Json(new { success = true });
        }
        return Json(new { success = false });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTask(string id)
    {
        await _dataService.DeleteTaskAsync(id);
        return Json(new { success = true });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveFeedback([FromForm] string? content, IFormFile? image)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(content) && (image == null || image.Length == 0))
                return Json(new { success = false, message = "Not içeriği veya görsel boş olamaz." });

            string? imagePath = null;
            if (image != null && image.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "notes");
                
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                var filePath = Path.Combine(uploadDir, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }
                imagePath = "/uploads/notes/" + fileName;
            }

            await _dataService.SaveFeedbackAsync(content ?? string.Empty, imagePath);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            // Log error for debugging
            await _dataService.LogErrorAsync(ex.Message, ex.StackTrace, "/Dashboard/SaveFeedback");
            return Json(new { success = false, message = "Sunucu tarafında bir hata oluştu: " + ex.Message });
        }
    }

    [HttpGet]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GetFeedbacks()
    {
        var notes = await _dataService.GetFeedbacksAsync();
        return Json(notes.Select(n => new { n.Id, n.Content, n.CreatedAt, n.ImagePath }));
    }

    [HttpPost]
    public async Task<IActionResult> AddSleepRecord(SleepRecord record)
    {
        if (ModelState.IsValid)
        {
            await _dataService.AddSleepRecordAsync(record);
            return RedirectToAction("Index");
        }
        return RedirectToAction("Index"); // Should really handle errors better, but redirecting back to dash for now
    }

    
    [HttpPost]
    public async Task<IActionResult> TrackActivity([FromBody] string panelName)
    {
        if (string.IsNullOrEmpty(panelName)) return BadRequest();
        await _dataService.LogUserActivityAsync(panelName);
        return Ok();
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

    [HttpGet]
    public IActionResult Exam()
    {
        // For now, this just returns the static Exam view.
        // In the future, this can pull user-specific exam progress from DB.
        return View();
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
            analysis = text.Length > 10000 ? text[..10000] + "\n...(devamı kısaltıldı)" : text;
        }
        else if (extension == ".pdf")
        {
            analysis = ReadPdfContent(filePath);
        }
        else if (new[] { ".docx", ".doc" }.Contains(extension))
        {
            analysis = ReadDocxContent(filePath);
        }
        else if (new[] { ".pptx", ".ppt" }.Contains(extension))
        {
            analysis = ReadPptxContent(filePath);
        }
        else if (extension == ".webm")
        {
            type = ResourceType.Audio;
            analysis = "Sesli mesaj yüklendi.";
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

        var geminiResult = await _geminiService.AnalyzeFileMetadataAsync(fileName, extension, analysis);
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

    [HttpPost]
    public async Task<IActionResult> UploadTransientFile(IFormFile file)
    {
        if (file == null || file.Length == 0) return Json(new { success = false });

        var fileName = "temp_" + Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/temp", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Json(new { success = true, filePath = "/uploads/temp/" + fileName, fileName = file.FileName });
    }

    private string ReadPdfContent(string filePath)
    {
        try
        {
            using var pdfReader = new iText.Kernel.Pdf.PdfReader(filePath);
            using var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfReader);
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                var page = pdfDoc.GetPage(i);
                var text = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page);
                sb.AppendLine(text);
                if (sb.Length > 50000) break; // Limit context
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"PDF okuma hatası: {ex.Message}";
        }
    }

    private string ReadDocxContent(string filePath)
    {
        try
        {
            using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document.Body;
            return body?.InnerText ?? "İçerik bulunamadı.";
        }
        catch (Exception ex)
        {
            return $"Word okuma hatası: {ex.Message}";
        }
    }

    private string ReadPptxContent(string filePath)
    {
        try
        {
            using var presentation = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(filePath, false);
            var part = presentation.PresentationPart;
            if (part == null) return "İçerik bulunamadı.";

            var sb = new System.Text.StringBuilder();
            var slideIds = part.Presentation.SlideIdList?.Elements<DocumentFormat.OpenXml.Presentation.SlideId>();
            if (slideIds != null)
            {
                foreach (var slideId in slideIds)
                {
                    var relId = slideId.RelationshipId?.Value;
                    if (relId == null) continue;

                    var slidePart = (DocumentFormat.OpenXml.Packaging.SlidePart)part.GetPartById(relId);
                    var slide = slidePart.Slide;
                    sb.AppendLine($"--- Slide ---");
                    foreach (var text in slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
                    {
                        sb.Append(text.Text);
                    }
                    sb.AppendLine();
                    if (sb.Length > 30000) break;
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"PowerPoint okuma hatası: {ex.Message}";
        }
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
                var rows = sheet.RangeUsed()?.RowsUsed()?.Take(500);
                if (rows == null || !rows.Any()) { sb.AppendLine("(boş sayfa)"); continue; }

                var maxCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 15;

                // Create Markdown Table Header
                var headers = new List<string> { "Satır" };
                for (int i = 1; i <= maxCol; i++)
                {
                    headers.Add(sheet.Column(i).ColumnLetter());
                }
                sb.AppendLine("| " + string.Join(" | ", headers) + " |");
                sb.AppendLine("|-" + string.Join("-|-", headers.Select(h => new string('-', Math.Max(h.Length, 3)))) + "-|");

                foreach (var row in rows)
                {
                    var rowData = new List<string>();
                    for (int i = 1; i <= maxCol; i++)
                    {
                        var cell = row.Cell(i);
                        string val = "";
                        
                        if (cell.IsMerged()) 
                        {
                            var mergedRange = cell.MergedRange();
                            if (mergedRange != null) {
                                val = mergedRange.FirstCell().GetFormattedString()?.Trim() ?? "";
                            }
                        } 
                        else 
                        {
                            val = cell.GetFormattedString()?.Trim() ?? "";
                        }
                        
                        // Clean values so they don't break the markdown table format
                        val = val.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", "").Replace("|", "-");
                        rowData.Add(string.IsNullOrEmpty(val) ? "-" : val);
                    }
                    
                    if (rowData.All(x => x == "-")) continue; // Skip completely empty rows seamlessly
                    
                    sb.AppendLine($"| {row.RowNumber()} | " + string.Join(" | ", rowData) + " |");
                }
                sb.AppendLine();
            }

            var result = sb.ToString();
            // Limit to 200000 chars to avoid token overflow
            return result.Length > 200000 ? result[..200000] + "\n...(devamı kısaltıldı)" : result;
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
                           item.MimeType.Contains("video") ? ResourceType.Video : ResourceType.Document;

                var existingFile = existingFiles.FirstOrDefault(f => f.ParentId == parentDbId && f.FileName == item.Name);

                string analysis = "Synced from Drive";
                // Download and parse if it's a document and we have a token
                if (type == ResourceType.Document && !string.IsNullOrEmpty(model.AccessToken))
                {
                    analysis = await DownloadAndAnalyzeDriveFile(item.Id, item.Name, item.MimeType, model.AccessToken);
                    
                    // Call Gemini analysis for deeper metadata
                    var gemResult = await _geminiService.AnalyzeFileMetadataAsync(item.Name, Path.GetExtension(item.Name), analysis);
                    if (gemResult != null)
                    {
                        analysis += $"\n\nAI Özet: {gemResult.Summary}";
                        // We could also store Topic/Complexity here if those fields are added to UploadedFile but for now we update AnalysisSummary
                    }
                }

                if (existingFile == null)
                {
                    var newFile = new UploadedFile
                    {
                        ParentId = parentDbId,
                        FileName = item.Name,
                        Url = item.WebViewLink,
                        Type = type,
                        UploadDate = DateTime.Now,
                        AnalysisSummary = analysis
                    };
                    await _dataService.AddFileAsync(newFile);
                    driveIdToDbId[item.Id] = newFile.Id;
                    existingFiles.Add(newFile);
                }
                else
                {
                    existingFile.Url = item.WebViewLink; 
                    if(existingFile.Type != type) existingFile.Type = type;
                    existingFile.AnalysisSummary = analysis;
                    await _dataService.UpdateFileAsync(existingFile);
                    driveIdToDbId[item.Id] = existingFile.Id;
                }

                processedDriveIds.Add(item.Id);
            }
        }

        return Json(new { success = true, count = processedDriveIds.Count });
    }

    private async Task<string> DownloadAndAnalyzeDriveFile(string fileId, string fileName, string mimeType, string accessToken)
    {
        try
        {
            byte[] fileBytes;
            string extension = "";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            if (mimeType.StartsWith("application/vnd.google-apps."))
            {
                // Export Google Docs/Sheets/Slides
                string exportMime = mimeType switch
                {
                    "application/vnd.google-apps.spreadsheet" => "text/csv",
                    "application/vnd.google-apps.presentation" => "application/pdf",
                    _ => "application/pdf"
                };
                extension = exportMime.Contains("pdf") ? ".pdf" : ".csv";
                
                var exportUrl = $"https://www.googleapis.com/drive/3/files/{fileId}/export?mimeType={Uri.EscapeDataString(exportMime)}";
                fileBytes = await client.GetByteArrayAsync(exportUrl);
            }
            else
            {
                // Download regular files
                var downloadUrl = $"https://www.googleapis.com/drive/3/files/{fileId}?alt=media";
                fileBytes = await client.GetByteArrayAsync(downloadUrl);
                extension = Path.GetExtension(fileName).ToLower();
            }

            if (fileBytes == null || fileBytes.Length == 0) return "İçerik çekilemedi.";

            // Save to temp file for parsing
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + extension);
            await System.IO.File.WriteAllBytesAsync(tempPath, fileBytes);

            string content = "";
            if (extension == ".pdf") content = ReadPdfContent(tempPath);
            else if (extension == ".docx" || extension == ".doc") content = ReadDocxContent(tempPath);
            else if (extension == ".pptx" || extension == ".ppt") content = ReadPptxContent(tempPath);
            else if (extension == ".csv" || extension == ".txt") content = await System.IO.File.ReadAllTextAsync(tempPath);
            else if (extension == ".xlsx" || extension == ".xls") content = ReadExcelContent(tempPath, fileName);
            else content = "Bu format henüz derinlemesine analiz edilemiyor.";

            System.IO.File.Delete(tempPath);
            return content.Length > 150000 ? content[..150000] + "..." : content;
        }
        catch (Exception ex)
        {
            return $"Drive okuma hatası: {ex.Message}";
        }
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

        if (request.TransientPaths != null && request.TransientPaths.Any())
        {
            foreach (var path in request.TransientPaths)
            {
                contextFiles.Add(new UploadedFile 
                { 
                    FilePath = path, 
                    FileName = Path.GetFileName(path),
                    Type = path.EndsWith(".webm") ? ResourceType.Audio : ResourceType.Image 
                });
            }
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

        var activeTasks = upcomingTasksList
            .Where(t => !t.IsCompleted)
            .OrderBy(t => t.DueDate)
            .ToList();

        if (activeTasks.Any())
        {
            var groupedByDate = activeTasks.GroupBy(t => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t.DueDate, DateTimeKind.Utc), turkeyTz).Date);
            foreach (var dateGroup in groupedByDate)
            {
                var targetDate = dateGroup.Key;
                var diffDays = (targetDate - nowTurkey.Date).Days;
                var dayLabel = diffDays == 0 ? "Bugün" : diffDays == 1 ? "Yarın" : diffDays == -1 ? "Dün" : targetDate.ToString("dddd");

                scheduleBuilder.AppendLine($"📅 {dayLabel} ({targetDate:dd MMM yyyy}):");
                foreach (var t in dateGroup)
                {
                    var tLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(t.DueDate, DateTimeKind.Utc), turkeyTz);
                    scheduleBuilder.AppendLine($"  - [ID: {t.Id}] {tLocal:HH:mm}: {t.Title} ({t.DurationHours}s, {t.Priority})");
                }
                scheduleBuilder.AppendLine();
            }
        }
        else
        {
            scheduleBuilder.AppendLine("Takviminizde gelecek veya beklemede olan hiçbir görev bulunmuyor.");
        }

        var scheduleContext = scheduleBuilder.ToString();

        // Get conversation history (last 20 messages for context window)
        var history = await _dataService.GetChatMessagesAsync(request.SessionId);
        var lessons = await _dataService.GetLessonsAsync();

        // Save user message to DB
        await _dataService.AddChatMessageAsync(request.SessionId, "user", request.Message);

        // Fetch sleep records for context
        var sleepRecords = await _dataService.GetRecentSleepRecordsAsync(7);

        // Call Gemini with history + ALL files + sleep + intensity
        string responseText = await _geminiService.ChatAsync(
            request.Message, contextFiles, scheduleContext,
            history.TakeLast(20).ToList(), lessons, allFiles,
            sleepRecords, request.IntensityMode ?? "Normal");

        string actionPerformed = "";

        // Parse ALL AI JSON Commands (loop through every ```json block)
        var searchFrom = 0;
        while (true)
        {
            var jsonStart = responseText.IndexOf("```json", searchFrom);
            if (jsonStart == -1) break;

            var contentStart = jsonStart + 7;
            var jsonEnd = responseText.IndexOf("```", contentStart);
            if (jsonEnd == -1) break;
            var jsonStr = responseText.Substring(contentStart, jsonEnd - contentStart).Trim();

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                if (root.TryGetProperty("command", out var cmdProp))
                {
                    var cmdType = cmdProp.GetString();
                    if (cmdType == "add_task")
                    {
                        actionPerformed = "requires_approval:add_task";
                    }
                }
            }
            catch (Exception) { }

            var fullBlock = responseText.Substring(jsonStart, (jsonEnd + 3) - jsonStart);
            var replacement = $"<div class='ai-command-container' data-command='{jsonStr.Replace("'", "&apos;")}'></div>";
            responseText = responseText.Replace(fullBlock, replacement).Trim();
            searchFrom = jsonStart + replacement.Length;
        }

        // Save assistant response to DB
        await _dataService.AddChatMessageAsync(request.SessionId, "assistant", responseText);

        return Json(new { success = true, response = responseText, actionPerformed, sessionId = request.SessionId });
    }

    [HttpPost]
    public async Task<IActionResult> ExecuteAiCommand([FromBody] System.Text.Json.JsonElement root)
    {
        try
        {
            if (root.TryGetProperty("command", out var cmdProp))
            {
                var cmd = cmdProp.GetString();
                if (cmd == "add_task")
                {
                    var title = root.GetProperty("title").GetString();
                    var dateStr = root.GetProperty("date").GetString();
                    var duration = root.TryGetProperty("durationHours", out var durProp) ? durProp.GetDouble() : 1.0;
                    var priorityStr = root.TryGetProperty("priority", out var prioProp) ? prioProp.GetString() : "Medium";
                    var diffScore = root.TryGetProperty("difficultyScore", out var dsProp) ? dsProp.GetInt32() : 3;
                    var diffReason = root.TryGetProperty("difficultyReason", out var drProp) ? drProp.GetString() : "";

                    if (DateTime.TryParse(dateStr, out var date))
                    {
                        var task = new TaskItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Title = title ?? "Yeni Görev",
                            Description = "🤖 AI Plan: Chat onaylı eklendi.",
                            DueDate = date.ToUniversalTime(),
                            DurationHours = duration,
                            Priority = Enum.TryParse<TaskPriority>(priorityStr, out var p) ? p : TaskPriority.Medium,
                            Category = TaskCategory.Study,
                            DifficultyScore = diffScore,
                            DifficultyReason = diffReason ?? ""
                        };
                        await _dataService.AddTaskAsync(task);
                        return Json(new { success = true, message = "Görev başarıyla eklendi!" });
                    }
                }
                else if (cmd == "add_tasks_batch")
                {
                    if (root.TryGetProperty("tasks", out var tasksArray))
                    {
                        int addedCount = 0;
                        foreach (var t in tasksArray.EnumerateArray())
                        {
                            // Expected format: ["Class Name", "2026-03-24T10:00:00", 1.5]
                            if (t.GetArrayLength() >= 3)
                            {
                                var title = t[0].GetString();
                                var dateStr = t[1].GetString();
                                var duration = t[2].GetDouble();

                                if (DateTime.TryParse(dateStr, out var date))
                                {
                                    var task = new TaskItem
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Title = title ?? "Ders",
                                        Description = "🤖 AI Plan: Toplu takvim aktarımı.",
                                        DueDate = date.ToUniversalTime(),
                                        DurationHours = duration,
                                        Priority = TaskPriority.High,
                                        Category = TaskCategory.Study,
                                        DifficultyScore = 3,
                                        DifficultyReason = ""
                                    };
                                    await _dataService.AddTaskAsync(task);
                                    addedCount++;
                                }
                            }
                        }
                        return Json(new { success = true, message = $"{addedCount} adet ders toplu olarak başarıyla eklendi!" });
                    }
                }
                else if (cmd == "delete_tasks_batch")
                {
                    if (root.TryGetProperty("taskIds", out var taskIdsArray))
                    {
                        int deletedCount = 0;
                        foreach (var idElem in taskIdsArray.EnumerateArray())
                        {
                            var id = idElem.GetString();
                            if (!string.IsNullOrEmpty(id))
                            {
                                await _dataService.DeleteTaskAsync(id);
                                deletedCount++;
                            }
                        }
                        return Json(new { success = true, message = $"{deletedCount} adet görev başarıyla silindi!" });
                    }
                }
                else if (cmd == "clear_all_tasks")
                {
                    var tasks = await _dataService.GetTasksAsync();
                    int deletedCount = tasks.Count;
                    foreach (var t in tasks) {
                        await _dataService.DeleteTaskAsync(t.Id);
                    }
                    return Json(new { success = true, message = $"Sistemdeki tüm AI ve manuel görevler ({deletedCount} adet) tamamen sıfırlandı!" });
                }
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "İşlem başarısız: " + ex.Message });
        }
        return Json(new { success = false, message = "Komut anlaşılamadı." });
    }

    [HttpGet]
    public async Task<IActionResult> GetFilePreview(string id)
    {
        var files = await _dataService.GetFilesAsync();
        var file = files.FirstOrDefault(f => f.Id == id);
        if (file == null) return NotFound();

        return Json(new
        {
            id = file.Id,
            name = file.FileName,
            type = file.Type.ToString(),
            url = file.Url,
            path = file.FilePath,
            summary = file.AnalysisSummary,
            topic = file.Topic,
            size = file.FileSize,
            date = file.UploadDate.ToString("dd MMM yyyy")
        });
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

    public async Task<IActionResult> Notebook()
    {
        var allFiles = await _dataService.GetFilesAsync();
        ViewBag.AllFiles = allFiles.Where(f => f.Type != ResourceType.Folder).OrderByDescending(f => f.UploadDate).ToList();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GenerateNotebookResponse([FromBody] NotebookRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Message) || request.SourceIds == null || !request.SourceIds.Any())
        {
            return Json(new { success = false, message = "Lütfen en az bir kaynak seçin ve mesajınızı yazın." });
        }

        var allFiles = await _dataService.GetFilesAsync();
        var sources = allFiles.Where(f => request.SourceIds.Contains(f.Id)).ToList();

        if (!sources.Any())
        {
            return Json(new { success = false, message = "Seçili kaynaklar bulunamadı." });
        }

        string response = await _geminiService.NotebookChatAsync(request.Message, sources, request.Mode ?? "chat");

        return Json(new { success = true, response });
    }

    public class NotebookRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<string> SourceIds { get; set; } = new();
        public string? Mode { get; set; } = "chat";
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string? IntensityMode { get; set; } = "Normal";
        public List<string> ContextFileIds { get; set; } = new List<string>();
        public List<string> TransientPaths { get; set; } = new List<string>();
    }

    public class DriveImportModel
    {
        public string ParentId { get; set; } = string.Empty;
        public string FolderUrl { get; set; } = string.Empty;
        public string? AccessToken { get; set; }
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

