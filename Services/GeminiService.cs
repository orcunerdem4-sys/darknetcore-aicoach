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
    /// Specialized mode for Notebook (Akıllı Defter) which is strictly grounded in selected sources.
    /// </summary>
    public async Task<string> NotebookChatAsync(
        string userMessage,
        List<UploadedFile> sources,
        string mode = "chat") // modes: chat, study_guide, audio_overview, flashcards
    {
        if (string.IsNullOrEmpty(_apiKey)) return "⚠️ Gemini API anahtarı eksik.";

        var sb = new StringBuilder();
        sb.AppendLine("Sen 'Dopamind Akıllı Defter' asistanısın. Görevin, sağlanan kaynakları kullanarak kullanıcıya yardımcı olmaktır.");
        sb.AppendLine("⚠️ KRİTİK KURAL: Sadece sağlanan kaynaklardaki bilgileri kullan. Eğer bilgi kaynaklarda yoksa, nazikçe 'Bu bilgi sağladığınız kaynaklarda bulunmuyor' de ve genel bilgi verme.");
        sb.AppendLine("Yanıtlarında tablo, madde listeleri ve alıntılar kullanarak bilgiyi organize et.");
        sb.AppendLine();

        sb.AppendLine("📁 SEÇİLİ KAYNAKLAR:");
        foreach (var source in sources)
        {
            sb.AppendLine($"- Dosya: {source.FileName}");
            if (!string.IsNullOrEmpty(source.AnalysisSummary))
                sb.AppendLine($"  Özet: {source.AnalysisSummary}");
            if (!string.IsNullOrEmpty(source.Topic))
                sb.AppendLine($"  Konu: {source.Topic}");
            // In a real RAG system, we'd inject chunked content here. 
            // For now, we rely on the metadata and potential future content extraction.
            sb.AppendLine();
        }

        if (mode == "audio_overview")
        {
            sb.AppendLine("🎧 PODCAST SENARYOSU GÖREVİ:");
            sb.AppendLine("Bu kaynakları tartışan iki uzman (Selin ve Can) arasında geçen 3-5 dakikalık bir podcast senaryosu yaz.");
            sb.AppendLine("Dinamik, merak uyandırıcı ve eğitici bir ton kullan. Mizah katabilirsin. Format:");
            sb.AppendLine("Selin: [Metin]");
            sb.AppendLine("Can: [Metin]");
            userMessage = "Seçili kaynaklara dayalı bir Audio Overview (Podcast) senaryosu oluştur.";
        }
        else if (mode == "study_guide")
        {
            sb.AppendLine("📖 ÇALIŞMA REHBERİ GÖREVİ:");
            sb.AppendLine("Bu kaynakları özetleyen yapılandırılmış bir rehber oluştur. Bölümler: Ana Kavramlar, Önemli Tarihler/İsimler, Detaylı Analiz ve Sınavda Çıkabilecek Yerler.");
            userMessage = "Seçili kaynaklara dayalı kapsamlı bir çalışma rehberi oluştur.";
        }
        else if (mode == "flashcards")
        {
            sb.AppendLine("🗂️ SORU-CEVAP KARTLARI GÖREVİ:");
            sb.AppendLine("Kaynaklardaki en önemli bilgilerden 10 adet Soru-Cevap kartı hazırla. Format: [Soru] - [Cevap].");
            userMessage = "Seçili kaynaklara dayalı soru-cevap kartları oluştur.";
        }

        var contents = new List<object> { new { role = "user", parts = new[] { new { text = userMessage } } } };

        // Use Pro model for deeper reasoning if available, otherwise fallback to flash
        return await CallGeminiRawAsync(contents, sb.ToString());
    }

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
        sb.AppendLine("Excel belgeleri olarak yüklenen ders programları, akademik takvime göre tarihleri içeren tablolardır.");
        sb.AppendLine("Tüm sayfaları/tarihleri okuyarak GÜNCEL haftayı ve bugünün tarihini bul. Programı haftalık ve günlük bazda doğru tarihle eşleştirerek kullan.");
        sb.AppendLine("Tarih bulamazsan veya genel bir iskeletse gün isimlerine (Pazartesi, Salı vb.) göre eşleştir.");
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
                if (!string.IsNullOrEmpty(f.AnalysisSummary))
                {
                    var summaryText = f.AnalysisSummary.Replace("\n", " ");
                    if (summaryText.Contains("AI Özet:")) 
                        summaryText = summaryText.Substring(summaryText.IndexOf("AI Özet:"));
                    sb.Append($" | Özet: {summaryText.Substring(0, Math.Min(250, summaryText.Length))}");
                }
                if (!string.IsNullOrEmpty(f.Url)) sb.Append($" | {f.Url}");
                sb.AppendLine();
            }
            sb.AppendLine();

            // Inject full parsed content for schedule/excel files so AI can read actual class data
            var scheduleFiles = filesToShow.Where(f =>
                !string.IsNullOrEmpty(f.AnalysisSummary) &&
                f.AnalysisSummary.Length > 200 &&
                !f.AnalysisSummary.StartsWith("External resource") &&
                (f.FileName.Contains(".xlsx") || f.FileName.Contains(".xls") ||
                 f.FileName.ToLower().Contains("dönem") || f.FileName.ToLower().Contains("program") ||
                 f.FileName.ToLower().Contains("schedule") || f.FileName.ToLower().Contains("takvim"))
            ).ToList();

            if (scheduleFiles.Any())
            {
                sb.AppendLine("📋 DERS PROGRAMI İÇERİKLERİ (Tam metin — dersleri buradan oku):");
                foreach (var f in scheduleFiles)
                {
                    sb.AppendLine($"=== {f.FileName} ===");
                    var snippet = f.AnalysisSummary!.Length > 150000
                        ? f.AnalysisSummary[..150000] + "\n...(devamı kısaltıldı)"
                        : f.AnalysisSummary;
                    sb.AppendLine(snippet);
                    sb.AppendLine();
                }
            }
        }

        if (contextFiles.Any())
        {
            sb.AppendLine("🎯 ODAKLANILAN DOSYALAR (Bu dosyaların içeriklerine tam hakimsin):");
            foreach (var cf in contextFiles)
            {
                if (!string.IsNullOrEmpty(cf.AnalysisSummary))
                {
                    sb.AppendLine($"--- {cf.FileName} İçeriği ---");
                    var snippet = cf.AnalysisSummary.Length > 150000 
                        ? cf.AnalysisSummary[..150000] + "\n...(devamı)" 
                        : cf.AnalysisSummary;
                    sb.AppendLine(snippet);
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"--- {cf.FileName} İçeriği ---\n(İçerik okunamadı veya boş)\n");
                }
            }
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
        sb.AppendLine("6. İNTERNET ERİŞİMİ YOK (404 HATASI VERME): Kendi başına URL'leri veya linkleri ziyaret edemezsin. '404 Not Found' hatası aldığını SÖYLEME. İçerikler sana doğrudan (özet veya tam metin olarak) yukarıda verilmiştir. Göremediklerin için 'içerik aktarılmadı' diyebilirsin ama asla linke gitmeye çalıştığını söyleme.");
        sb.AppendLine("7. ASLA SOHBETİ UZATMA: 'Başka bir sorun var mı', 'Yardımcı olabileceğim başka bir şey', 'Daha fazla sorun olursa buradayım' gibi klişe sohbet bitiş/giriş cümleleri KESİNLİKLE KULLANMA. Sen bir sohbet botu değil, doğrudan isteneni yapan, lafı uzatmayan bir veri işleme ve aksiyon motorusun.");
        sb.AppendLine();
        sb.AppendLine("🛠️ GÖREV EKLEME KOMUTLARI:");
        sb.AppendLine("Takvime işlem yapman gerekiyorsa MUTLAKA ```json ve ``` kullan (aksi takdirde çalışmaz).");
        sb.AppendLine("1. TEKLİ GÖREV: `add_task`. Örnek: ```json\n{ \"command\": \"add_task\", \"title\": \"Matematik\", \"date\": \"2026-03-13T14:00:00\", \"durationHours\": 1.5, \"priority\": \"High\", \"difficultyScore\": 7, \"difficultyReason\": \"-\" }\n```");
        sb.AppendLine("2. TOPLU DÖNEMLİK TAKVİM KAYDI: Kullanıcı senden bir belgedeki tüm programı dönemin geri kalanı için topluca kaydetmeni isterse SAKIN tek tek `add_task` yazarak işlemi yarıda kesme! Sınırı aşmamak için şu süper-kısa Array formatını kullan: ");
        sb.AppendLine("Örnek: ```json\n{ \"command\": \"add_tasks_batch\", \"tasks\": [ [\"Matematik\", \"2026-03-24T10:00:00\", 2.0], [\"Fizik\", \"2026-03-25T14:30:00\", 1.5] ] }\n```");
        sb.AppendLine("  -> Bu formatta `[Ders Adı, ISO TarihSaati, Süre(Saat)]` şeklinde Array içinde Array kullan. Belgedeki HİÇBİR dersi atlamadan listeyi tamamla. Bu format aşırı kısadır, token sınırında korkmadan son güne kadar asla yarım bırakma.");
        sb.AppendLine("⚠️ ZAMAN DİLİMİ UYARISI: Görev eklerken ISO formatlı saatlerde KESİNLİKLE saatten çıkarma veya toplama yapma (UTC hesabı yapma)! Belgede gördüğün yerel saati direkt olarak yaz (Örn 08:40 ise direkt `T08:40:00` olarak yaz, 'Z' harfi ekleme).");
        sb.AppendLine("🛠️ GÖREV SİLME KOMUTLARI:");
        sb.AppendLine("DİKKAT KİRİTİK KURAL: Görevleri silmek için kullanıcıya sadece 'sildim' yanıtı dönmen YETMEZ! Arkada çalışan sistemin bunu anlaması için KESİNLİKLE VE HER ZAMAN yanıtının sonuna bir ```json bloğu eklemek ZORUNDASIN. Json bloğu yazmazsan görevler SİLİNMEZ!");
        sb.AppendLine("1. KISMEN SİLME (Bazılarını sil): `delete_tasks_batch`. Örnek çıktın şöyle olmalı: \n```json\n{ \"command\": \"delete_tasks_batch\", \"taskIds\": [\"id1\", \"id2\"] }\n```");
        sb.AppendLine("2. TAMAMEN SIFIRLAMA (Her şeyi sil): Kullanıcı tüm takvimi silmeni isterse, tek kelime bile etmeden veya onay mesajından hemen sonra ŞU BLOĞU YAZMAK ZORUNDASIN:");
        sb.AppendLine("```json\n{ \"command\": \"clear_all_tasks\" }\n```");
        sb.AppendLine("Eğer bu bloğu yazmazsan, sistem çalışmayacaktır.");

        var systemPrompt = sb.ToString();

        // Build contents array for multimodal input
        var contentsList = new List<object>();

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

        return await CallGeminiRawAsync(contentsList, systemPrompt);
    }

    private async Task<string> CallGeminiRawAsync(List<object> contents, string? systemPrompt = null)
    {
        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={_apiKey}";
            
            var requestBody = new Dictionary<string, object>
            {
                { "contents", contents },
                { "generationConfig", new { temperature = 0.7, maxOutputTokens = 8192 } }
            };

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                requestBody["system_instruction"] = new { parts = new[] { new { text = systemPrompt } } };
            }

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
