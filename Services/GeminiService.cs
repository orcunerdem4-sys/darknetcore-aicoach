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
        List<UploadedFile>? allFiles = null)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "⚠️ Gemini API anahtarı eksik.";

        // Build system context
        var sb = new StringBuilder();
        sb.AppendLine("Sen 'AI Coach' isimli uzman bir kişisel ders asistanısın. Türkçe konuşuyorsun.");
        sb.AppendLine("Kullanıcının tüm ders materyallerini, dosyalarını, görevlerini ve programını biliyorsun.");
        sb.AppendLine("Bu bilgilerle kişiselleştirilmiş, somut öneriler sunuyorsun.");
        sb.AppendLine();
        sb.AppendLine("⚠️ ÖNEMLI KURAL - Excel Ders Programı Yorumlama:");
        sb.AppendLine("Yüklenen Excel dosyaları (özellikle 'Dönem', 'Program', 'Schedule' gibi isimler taşıyanlar)");
        sb.AppendLine("ÜNİVERSİTE HAFTALIK DERS PROGRAMI ŞABLONLARIdır. Bu dosyalar:");
        sb.AppendLine("- Belirli bir tarihe değil, haftanın günlerine (Pazartesi, Salı... / Mon, Tue...) göre organize edilmiştir.");
        sb.AppendLine("- Her hafta tekrar eden dersleri gösterir. Dosyada 'Feb 9-13' gibi bir tarih görüyorsan bu yalnızca örnek haftadır.");
        sb.AppendLine("- 'Yarın Salı' diye sorulduğunda: dosyadaki SALI sütununa/satırına bak, tarihe değil GÜNE göre yanıtla.");
        sb.AppendLine("- Dosyada iki sütun yan yana varsa (Türkçe program | İngilizce program), kullanıcı İngilizce programı takip ediyor.");
        sb.AppendLine("- EĞER sana verilen dosya özetinde (AnalysisSummary) saatler veya ders içerikleri varsa bunlara dayanarak analiz yap.");
        sb.AppendLine("- EĞER 'Synced from Drive' dışında hiçbir içerik yoksa veya 'Hata' mesajı görüyorsan, kullanıcıya içeriği okuyamadığını dürüstçe söyle.");

        if (lessons != null && lessons.Any())
        {
            sb.AppendLine("📚 Kullanıcının Dersleri:");
            foreach (var l in lessons)
                sb.AppendLine($"  - {l.Name}");
            sb.AppendLine();
        }

        // ALL files — full knowledge base
        var filesToShow = allFiles?.Where(f => f.Type != ResourceType.Folder).ToList();
        if (filesToShow != null && filesToShow.Any())
        {
            sb.AppendLine(" Kullanıcının Tüm Yüklü Dosyaları:");
            foreach (var f in filesToShow)
            {
                sb.Append($"  • [{f.Type}] {f.FileName}");
                if (!string.IsNullOrEmpty(f.Topic)) sb.Append($" | Konu: {f.Topic}");
                if (!string.IsNullOrEmpty(f.AnalysisSummary)) sb.Append($" | Özet: {f.AnalysisSummary}");
                if (f.EstimatedStudyTime > 0) sb.Append($" | Süre: {f.EstimatedStudyTime}s");
                if (f.ComplexityScore > 0) sb.Append($" | Zorluk: {f.ComplexityScore}/10");
                if (!string.IsNullOrEmpty(f.Url)) sb.Append($" | Link: {f.Url}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        if (contextFiles.Any())
        {
            sb.AppendLine("🎯 Şu an odaklanılan dosyalar:");
            foreach (var f in contextFiles)
                sb.AppendLine($"  - {f.FileName}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(userScheduleContext))
        {
            sb.AppendLine("📅 Takvim:");
            sb.AppendLine(userScheduleContext);
            sb.AppendLine();
        }

        sb.AppendLine("Yanıtlarında markdown kullan. Kısa ve net ol.");
        sb.AppendLine("Kullanıcı dosyalarını sorarsa yukarıdaki listedeki 'Özet' (AnalysisSummary) kısmına bakarak cevap ver. İçerik verilmişse o dosyayı 'okuyabildiğini' varsay ve konuyu derinleştir.");
        sb.AppendLine();
        sb.AppendLine("🛠️ KOMUT KURALLARI (GÖREV EKLEME/SİLME):");
        sb.AppendLine("Eğer takvime bir şey eklemen veya silmen gerekiyorsa:");
        sb.AppendLine("1. MUTLAKA ```json ve ``` işaretlerini kullan. Bu işaretler olmadan komutların ÇALIŞMAZ.");
        sb.AppendLine("2. Her görev için AYRI bir ```json bloğu yaz.");
        sb.AppendLine("3. Örnek Ekleme: ```json\n{ \"command\": \"add_task\", \"title\": \"Matematik Ödevi\", \"date\": \"2026-03-11T14:00:00\", \"durationHours\": 2.0, \"priority\": \"High\", \"difficultyScore\": 7, \"difficultyReason\": \"Anatomi ezberi yoğun olduğu için bölünmeden 2 saat çalışılmalı.\" }\n```");
        sb.AppendLine("4. Örnek Silme: ```json\n{ \"command\": \"remove_task\", \"title\": \"Matematik Ödevi\" }\n```");
        sb.AppendLine("⚠️ ÖNEMLİ: JSON bloğunu ekledikten sonra kullanıcıya sadece 1-2 cümlelik nezaket dolu bir onay mesajı ver. JSON içeriğini mesajın içinde HAM METİN olarak asla tekrar etme.");

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
