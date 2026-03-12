using System.Text;
using System.Text.Json;
using DarkNetCore.Models;

namespace DarkNetCore.Services;

public class GeminiService
{
    private readonly string _apiKey;
    private const string ModelName = "gemini-2.5-flash";
    private readonly HttpClient _httpClient;

    public GeminiService(IConfiguration config, HttpClient httpClient)
    {
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                  ?? config["Gemini:ApiKey"] ?? string.Empty;
        _httpClient = httpClient;
    }

    public async Task<GeminiAnalysisResult?> AnalyzeFileMetadataAsync(string fileName, string fileType, string? content = null)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;

        var contentPreview = string.IsNullOrEmpty(content) ? "" : $"\nFile Content Preview: {content.Substring(0, Math.Min(content.Length, 5000))}";

        var prompt = $@"Analyze this file for a study plan.
File Name: '{fileName}'
Type: {fileType}{contentPreview}

Return a JSON object with these fields:
- topic: (string) The main academic subject (e.g. 'Physics', 'History', 'Programming', 'Language').
- complexityScore: (int) 1-10 difficulty rating. 
- wordCount: (int) Estimated word count (if applicable).
- estimatedHours: (double) Time to study deeply.
- summary: (string) A strict 1-sentence summary (e.g. 'Voice message from student' or 'Meeting recording').

Example JSON: {{ ""topic"": ""Biology"", ""complexityScore"": 7, ""wordCount"": 5000, ""estimatedHours"": 2.5, ""summary"": ""Covers cellular respiration process."" }}";

        var contents = new List<object> { new { role = "user", parts = new[] { new { text = prompt } } } };
        var response = await CallGeminiRawAsync(contents);
        return ParseGeminiJson<GeminiAnalysisResult>(response);
    }

    /// <summary>
    /// Multi-turn chat with full user context (all files, lessons, schedule).
    /// </summary>
    public async Task<string> ChatAsync(
        string userMessage,
        List<UploadedFile> contextFiles,
        string userScheduleContext,
        List<ChatMessage>? history = null,
        List<Lesson>? lessons = null,
        List<UploadedFile>? allFiles = null,
        List<SleepRecord>? sleepRecords = null,
        string intensityMode = "Normal")
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "⚠️ Gemini API anahtarı eksik.";

        // Build system context
        var sb = new StringBuilder();
        sb.AppendLine("Sen 'Dopamind AI Coach' isimli akıllı bir kişisel asistansın. Türkçe konuşuyorsun.");
        sb.AppendLine("Kullanıcının sistemdeki TÜM verilerine (takvim, dosyalar, ders programı, uyku kayıtları) erişimin var.");
        sb.AppendLine("Yanıtların kısa, net ve görsel olarak zengin olmalı. Uzun paragraflar yerine tablolar ve kutucuklar kullan.");
        sb.AppendLine();

        // Intensity Mode - only hours/day guidance, no lifestyle prescriptions
        var intensityHours = intensityMode switch
        {
            "Light"   => "3-4",
            "Normal"  => "5-6",
            "Intense" => "7-8",
            "Max"     => "9+",
            _         => "5-6"
        };
        sb.AppendLine($"🎯 Seçili Çalışma Temposu: **{intensityMode}** — Günde yaklaşık {intensityHours} saat net çalışma.");
        sb.AppendLine("→ Program yaparken bu süreyi hedef al; sistemdeki mevcut görevleri, ders saatlerini ve uyku verilerini dikkate alarak boşlukları doldur.");
        sb.AppendLine();

        sb.AppendLine("📋 EXCEL DERS PROGRAMI KURALI:");
        sb.AppendLine("'Dönem/Program/Schedule' gibi Excel dosyaları haftalık tekrar eden ders şablonlarıdır.");
        sb.AppendLine("Tarih değil GÜN bazlı oku. İki sütun varsa İngilizce programı takip et.");
        sb.AppendLine("İçerik okunabiliyorsa kullan; 'Synced from Drive' veya hata mesajı varsa kullanıcıya söyle.");
        sb.AppendLine();

        if (sleepRecords != null && sleepRecords.Any())
        {
            var avgSleep = Math.Round(sleepRecords.Average(s => s.TotalHours), 1);
            var latest = sleepRecords.OrderByDescending(s => s.SleepEnd).FirstOrDefault();
            sb.AppendLine($"😴 Uyku (Son 7 Gün): Ortalama {avgSleep} saat/gece.");
            if (latest != null)
            {
                var turkeyTz2 = TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
                var sleepEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(latest.SleepEnd, DateTimeKind.Utc), turkeyTz2);
                sb.AppendLine($"  Son kalkış tahmini: ~{sleepEnd:HH:mm} → Program bu saatten başlatılabilir.");
            }
            sb.AppendLine();
        }

        if (lessons != null && lessons.Any())
        {
            sb.AppendLine("📚 Kayıtlı Dersler: " + string.Join(", ", lessons.Select(l => l.Name)));
            sb.AppendLine();
        }

        // ALL files — full knowledge base
        var filesToShow = allFiles?.Where(f => f.Type != ResourceType.Folder).ToList();
        if (filesToShow != null && filesToShow.Any())
        {
            sb.AppendLine("📁 Yüklü Dosyalar:");
            foreach (var f in filesToShow)
            {
                sb.Append($"  • [{f.Type}] **{f.FileName}**");
                if (!string.IsNullOrEmpty(f.Topic)) sb.Append($" ({f.Topic})");
                if (f.EstimatedStudyTime > 0) sb.Append($" ~{f.EstimatedStudyTime}s");
                if (f.ComplexityScore > 0) sb.Append($" Zorluk:{f.ComplexityScore}/10");
                if (!string.IsNullOrEmpty(f.AnalysisSummary) && f.AnalysisSummary.Length < 200)
                    sb.Append($" | {f.AnalysisSummary.Substring(0, Math.Min(200, f.AnalysisSummary.Length))}");
                if (!string.IsNullOrEmpty(f.Url)) sb.Append($" | {f.Url}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        if (contextFiles.Any())
        {
            sb.AppendLine("🎯 Odaklanılan dosyalar: " + string.Join(", ", contextFiles.Select(f => f.FileName)));
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(userScheduleContext))
        {
            sb.AppendLine("📅 Takvim:");
            sb.AppendLine(userScheduleContext);
            sb.AppendLine();
        }

        sb.AppendLine("─── YANIT FORMATI KURALLARI ───");
        sb.AppendLine("1. PROGRAM/TABLO: Ders programları, haftalık planlar, zaman blokları → MUTLAKA markdown tablosu kullan (| Saat | Aktivite | Not |).");
        sb.AppendLine("2. GÖREV KARTLARI: Takvime eklenen/çıkarılan görevler → JSON bloğundan önce `> 📌 [Görev Adı] — [Tarih]` şeklinde alıntı satırı ile özetle.");
        sb.AppendLine("3. DOSYA ADI: Bir dosyadan bahsederken `dosya_adi.pdf` şeklinde kod formatında yaz.");
        sb.AppendLine("4. UZUN YAZILARDAN KAÇIN: Madde listesi veya tablo kullan. Paragraf ancak açıklama gerektirdiğinde.");
        sb.AppendLine("5. KISA TUT: Yanıtlar mümkün olduğunca öz olsun. Kullanıcı zaten verileri biliyor; analiz ve yönlendirme ver.");
        sb.AppendLine();
        sb.AppendLine("🛠️ GÖREV EKLEME/SİLME KOMUTLARI:");
        sb.AppendLine("Takvime işlem yapman gerekiyorsa MUTLAKA ```json ve ``` kullan (aksi takdirde çalışmaz).");
        sb.AppendLine("Her görev için ayrı ```json bloğu. Ekledikten sonra 1-2 cümle onay mesajı ver.");
        sb.AppendLine("Örnek: ```json\n{ \"command\": \"add_task\", \"title\": \"Farmakoloji Tekrar\", \"date\": \"2026-03-13T14:00:00\", \"durationHours\": 1.5, \"priority\": \"High\", \"difficultyScore\": 7, \"difficultyReason\": \"Yoğun konu.\" }\n```");
        sb.AppendLine("⚠️ JSON bloğunu yazdıktan sonra ham metnini mesajda asla tekrar etme.");

        var systemPrompt = sb.ToString();

        // Build contents array for multimodal input
        var contentsList = new List<object>();

        // 1. System Prompt
        contentsList.Add(new { role = "user", parts = new[] { new { text = systemPrompt } } });
        contentsList.Add(new { role = "model", parts = new[] { new { text = "Anlaşıldı! Sana yardımcı olmaya hazırım." } } });

        // 2. History
        if (history != null)
        {
            foreach (var h in history.OrderBy(m => m.SentAt))
            {
                contentsList.Add(new { role = h.Role == "assistant" ? "model" : "user", parts = new[] { new { text = h.Content } } });
            }
        }

        // 3. Current Turn with Multimodal Context
        var currentParts = new List<object>();
        currentParts.Add(new { text = userMessage });

        foreach (var file in contextFiles)
        {
            var extension = Path.GetExtension(file.FileName).ToLower();
            string? mimeType = extension switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".webm" => "audio/webm", _ => null };

            if (mimeType != null && !string.IsNullOrEmpty(file.FilePath))
            {
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    var base64 = Convert.ToBase64String(await System.IO.File.ReadAllBytesAsync(fullPath));
                    currentParts.Add(new { inline_data = new { mime_type = mimeType, data = base64 } });
                }
            }
        }

        contentsList.Add(new { role = "user", parts = currentParts.ToArray() });

        return await CallGeminiRawAsync(contentsList);
    }

    private async Task<string> CallGeminiRawAsync(List<object> contents)
    {
        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={_apiKey}";
            var requestBody = new { contents, generationConfig = new { temperature = 0.7, maxOutputTokens = 8192 } };
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, jsonContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return $"⚠️ API Hatası: {response.StatusCode} - {responseString}";

            var jsonDocument = JsonDocument.Parse(responseString);
            return jsonDocument.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
        }
        catch (Exception ex) { return $"⚠️ Hata: {ex.Message}"; }
    }

    private T? ParseGeminiJson<T>(string rawText) where T : class
    {
        if (string.IsNullOrEmpty(rawText) || rawText.StartsWith("⚠️")) return null;
        try
        {
            var cleanJson = rawText.Replace("```json", "").Replace("```", "").Trim();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(cleanJson, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JSON Parse Error: {ex.Message}");
            return null;
        }
    }
}

public class GeminiAnalysisResult
{
    public string? Topic { get; set; }
    public int ComplexityScore { get; set; }
    public int WordCount { get; set; }
    public double EstimatedHours { get; set; }
    public string? Summary { get; set; }
}
