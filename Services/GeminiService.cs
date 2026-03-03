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

    public async Task<GeminiAnalysisResult?> AnalyzeFileMetadataAsync(string fileName, string fileType)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;

        var prompt = $@"Analyze this file based on its name and type for a study plan.
File Name: '{fileName}'
Type: {fileType}

Return a JSON object with these fields:
- topic: (string) The main academic subject (e.g. 'Physics', 'History').
- complexityScore: (int) 1-10 difficulty rating. 
- wordCount: (int) Estimated word count.
- estimatedHours: (double) Time to study deeply.
- summary: (string) A very short, strict 1-sentence summary of what this file likely contains.

Example JSON: {{ ""topic"": ""Biology"", ""complexityScore"": 7, ""wordCount"": 5000, ""estimatedHours"": 2.5, ""summary"": ""Covers cellular respiration process."" }}";

        var response = await CallGeminiRestAsync(prompt, new List<(string role, string text)>());
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
        sb.AppendLine("- Excel içeriğindeki ders adlarını, saatleri ve hocaları AYNEN kullan. 'Bilmiyorum' deme.");

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
            sb.AppendLine("� Kullanıcının Tüm Yüklü Dosyaları:");
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
        sb.AppendLine("Kullanıcı dosyalarını sorarsa yukarıdaki listeden cevap ver — asla 'göremiyorum' veya 'bilmiyorum' deme.");
        sb.AppendLine();
        sb.AppendLine("🛠️ GÖREV VE TAKVİM KOMUTLARI:");
        sb.AppendLine("Eğer kullanıcı senden takvimine/programına yeni bir görev EKLENMESİNİ veya SİLİNMESİNİ açıkça isterse, yanıtının \nEN SONUNA aşağıdaki gibi bir JSON bloğu koy (mutlaka ```json ve ``` arasında):");
        sb.AppendLine("Ekleme için: ```json\n{ \"command\": \"add_task\", \"title\": \"Anatomi Çalışması\", \"date\": \"2026-03-03T14:00:00\", \"durationHours\": 2.0, \"priority\": \"High\" }\n```");
        sb.AppendLine("Silme için: ```json\n{ \"command\": \"remove_task\", \"title\": \"Anatomi Çalışması\" }\n```");
        sb.AppendLine("Bu JSON'ı sadece aksiyon gerektiğinde kullan. Bu JSON'u koyarsan sistem arka planda görevi ekler/siler. Ayrıca kullanıcıya \"Görev takvimine eklendi\" gibi doğal dilde bir onay cümlesi de yaz.");

        var systemPrompt = sb.ToString();

        // Build conversation turns from history
        var turns = new List<(string role, string text)>();
        turns.Add(("user", systemPrompt)); // Inject system context as first user turn
        turns.Add(("model", "Anlaşıldı! Tüm dosyalarını ve ders programını inceledim. Sana nasıl yardımcı olabilirim?"));

        if (history != null)
        {
            foreach (var msg in history.OrderBy(m => m.SentAt))
            {
                var role = msg.Role == "assistant" ? "model" : "user";
                turns.Add((role, msg.Content));
            }
        }

        return await CallGeminiRestAsync(userMessage, turns);
    }

    private async Task<string> CallGeminiRestAsync(string userMessage, List<(string role, string text)> history)
    {
        try
        {
            Console.WriteLine($"[Gemini] Sending to {ModelName} with {history.Count} history turns...");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={_apiKey}";

            // Build contents array from history + current message
            var contentsList = new List<object>();
            foreach (var (role, text) in history)
            {
                contentsList.Add(new
                {
                    role,
                    parts = new[] { new { text } }
                });
            }
            // Add current user message
            contentsList.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            var requestBody = new
            {
                contents = contentsList,
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 8192
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, jsonContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Gemini] API ERROR: {response.StatusCode} - {responseString}");

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    return "⚠️ **Servis Yoğunluğu:** Lütfen 1 dakika bekleyin. (429)";

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return $"⚠️ **Model Hatası:** '{ModelName}' bulunamadı. (404)";

                return $"⚠️ **API Hatası:** {response.StatusCode}";
            }

            var jsonDocument = JsonDocument.Parse(responseString);
            var textResult = jsonDocument.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();

            return string.IsNullOrEmpty(textResult)
                ? "⚠️ Yanıt boş döndü."
                : textResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gemini] EXCEPTION: {ex.Message}");
            return $"⚠️ **Hata:** {ex.Message}";
        }
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
