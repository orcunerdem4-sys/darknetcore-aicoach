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

    /// <summary>
    /// Dosya yüklendiğinde derin analiz yapar.
    /// Sayfa/Kelime yoğunluğu, Görsel oranı ve Konu ağırlığına göre skor belirler.
    /// PDF ve görseller doğrudan Vision API ile okunur (Base64).
    /// </summary>
    public async Task<GeminiAnalysisResult?> AnalyzeFileMetadataAsync(string fileName, string fileType, string? content = null, string? filePath = null)
    {
        if (string.IsNullOrEmpty(_apiKey)) return null;

        var contentPreview = string.IsNullOrEmpty(content) ? "" : $"\nMetin Önizlemesi:\n{content.Substring(0, Math.Min(content.Length, 5000))}";

        // ZORLUK SKORU VE PARAMETRE KURALLARI
        var prompt = $@"Aşağıdaki dosyayı bir öğrenci çalışma planı için derinlemesine analiz et.
Dosya Adı: '{fileName}'
Tür: {fileType}{contentPreview}

GÖREV:
Dosyayı ve (eğer eklendiyse) görsel içeriğini incele. ZORLUK SEVİYESİNİ (complexityScore) ASLA VARSAYILAN OLARAK 1 VERME.
Aşağıdaki parametreleri kullanarak objektif bir değerlendirme yap:
1. Yoğunluk: Sayfa başına düşen kelime sayısı ve toplam sayfa/metin hacmi.
2. Format: Görsel/Şema oranı. Görsel öğrenen biri için şemalar kolaylaştırıcı, metin odaklı biri için zorlaştırıcı olabilir.
3. Akademik Ağırlık: Anatomi, Fizyoloji, İleri Matematik (Türev/İntegral) gibi konuların doğuştan gelen zorluk yükünü hesaba kat.

Yanıtını MUTLAKA şu JSON formatında dön:
- topic: (string) Ana konu başlığı.
- complexityScore: (int) 1 ile 10 arasında gerçekçi zorluk derecesi.
- wordCount: (int) Tahmini kelime sayısı.
- estimatedHours: (double) Bu materyali tam öğrenmek için gereken tahmini net saat.
- summary: (string) İçeriği özetleyen tek bir cümle.";

        var parts = new List<object> { new { text = prompt } };

        // 📸 VISION DESTEĞİ: PDF ve Görseller doğrudan AI'ya Base64 olarak gönderiliyor
        if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
        {
            var ext = Path.GetExtension(filePath).ToLower();
            string? mimeType = ext switch
            {
                ".pdf"  => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"  => "image/png",
                ".webp" => "image/webp",
                _ => null
            };

            if (mimeType != null)
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var base64 = Convert.ToBase64String(fileBytes);
                parts.Add(new { inline_data = new { mime_type = mimeType, data = base64 } });
            }
        }

        var contents = new List<object> { new { role = "user", parts = parts.ToArray() } };
        var response = await CallGeminiRawAsync(contents);
        return ParseGeminiJson<GeminiAnalysisResult>(response);
    }

    /// <summary>
    /// Akıllı Defter Modu: Seçili kaynaklara sadık kalarak cevap üretir.
    /// </summary>
    public async Task<string> NotebookChatAsync(
        string userMessage,
        List<UploadedFile> sources,
        string mode = "chat")
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
        return await CallGeminiRawAsync(contents, sb.ToString());
    }

    /// <summary>
    /// Ana Chat Fonksiyonu: Takvim, Uyku, Dosyalar ve Planlama yeteneklerini birleştirir.
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

        var sb = new StringBuilder();
        sb.AppendLine("Sen 'Dopamind AI Coach' isimli akıllı bir kişisel asistansın. Türkçe konuşuyorsun.");
        sb.AppendLine("Kullanıcının sistemdeki TÜM verilerine (takvim, dosyalar, ders programı, uyku kayıtları) erişimin var.");
        sb.AppendLine("Yanıtların kısa, net ve görsel olarak zengin olmalı. Uzun paragraflar yerine tablolar ve kutucuklar kullan.");
        sb.AppendLine();

        // Çalışma Temposu
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

        // 🕐 ZAMAN VE UYKU KURALLARI
        if (sleepRecords != null && sleepRecords.Any())
        {
            var avgSleep = Math.Round(sleepRecords.Average(s => s.TotalHours), 1);
            var latest = sleepRecords.OrderByDescending(s => s.SleepEnd).FirstOrDefault();
            sb.AppendLine($"😴 Uyku (Son 7 Gün): Ortalama {avgSleep} saat/gece.");
            if (latest != null)
            {
                var turkeyTz = TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
                var sleepEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(latest.SleepEnd, DateTimeKind.Utc), turkeyTz);
                sb.AppendLine($"  Son kalkış tahmini: ~{sleepEnd:HH:mm} → Program bu saatten başlatılabilir.");
            }
            sb.AppendLine();
        }
        sb.AppendLine("⚠️ ZAMAN KURALI: Varsayılan olarak 07:00'den önce kalkılmaz, 01:00'den sonra uyunur. Kullanıcı gece çalışacağını belirtmedikçe bu saatler dışına plan yapma.");
        sb.AppendLine();

        if (lessons != null && lessons.Any())
        {
            sb.AppendLine("📚 Kayıtlı Dersler: " + string.Join(", ", lessons.Select(l => l.Name)));
            sb.AppendLine();
        }

        // Tüm dosyalar - tam bilgi bankası
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

            // Ders programı / Excel dosyalarının tam içeriğini enjekte et
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

        // Seçili / odaklanılan dosyaların tam içeriği
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
        sb.AppendLine("7. ASLA SOHBETİ UZATMA: 'Başka bir sorun var mı', 'Yardımcı olabileceğim başka bir şey' gibi klişe sohbet bitiş cümleleri KESİNLİKLE KULLANMA. Sen bir veri işleme ve aksiyon motorusun.");
        sb.AppendLine();

        sb.AppendLine("🛠️ GÖREV VE TAKVİM KOMUTLARI:");
        sb.AppendLine("Takvime işlem yapman gerekiyorsa MUTLAKA ```json ve ``` kullan (aksi takdirde çalışmaz).");
        sb.AppendLine("1. TEKLİ GÖREV EKLE: `add_task`. Örnek: ```json\n{ \"command\": \"add_task\", \"title\": \"Matematik\", \"date\": \"2026-03-13T14:00:00\", \"durationHours\": 1.5, \"priority\": \"High\", \"difficultyScore\": 7, \"difficultyReason\": \"-\" }\n```");
        sb.AppendLine("2. GÖREV DÜZENLE: `update_task`. Örnek: ```json\n{ \"command\": \"update_task\", \"taskId\": \"ID\", \"date\": \"2026-03-24T15:00:00\" }\n```");
        sb.AppendLine("3. TOPLU DÖNEMLİK TAKVİM KAYDI: Kullanıcı senden bir belgedeki tüm programı topluca kaydetmeni isterse SAKIN tek tek `add_task` yazarak işlemi yarıda kesme! Şu süper-kısa Array formatını kullan:");
        sb.AppendLine("Örnek: ```json\n{ \"command\": \"add_tasks_batch\", \"tasks\": [ [\"Matematik\", \"2026-03-24T10:00:00\", 2.0], [\"Fizik\", \"2026-03-25T14:30:00\", 1.5] ] }\n```");
        sb.AppendLine("  -> Bu formatta `[Ders Adı, ISO TarihSaati, Süre(Saat)]` şeklinde Array içinde Array kullan. Belgedeki HİÇBİR dersi atlamadan listeyi tamamla.");
        sb.AppendLine("  ⚠️ KRİTİK: `add_tasks_batch` kullanırken ÖNCE uzun bir markdown tablosu YAZMA! Sadece 1-2 satır özet yaz, sonra direkt JSON bloğuna geç. Tablo yazmak token kapasitesini boşa harcar ve yanıt yarım kalır!");
        sb.AppendLine("⚠️ ZAMAN DİLİMİ UYARISI: Görev eklerken ISO saatlerde KESİNLİKLE UTC hesabı (toplama/çıkarma) YAPMA! Belgede gördüğün yerel saati direkt yaz (Örn 08:40 ise `T08:40:00`, 'Z' harfi ekleme).");
        sb.AppendLine();
        sb.AppendLine("🛠️ GÖREV SİLME KOMUTLARI:");
        sb.AppendLine("DİKKAT KRİTİK KURAL: Görevleri silmek için 'sildim' yanıtı dönmen YETMEZ! Sistemin bunu anlaması için MUTLAKA yanıtının sonuna bir ```json bloğu eklemek ZORUNDASIN. Json bloğu yazmazsan görevler SİLİNMEZ!");
        sb.AppendLine("1. KISMEN SİLME (5-10 görev): `delete_tasks_batch`. Örnek: ```json\n{ \"command\": \"delete_tasks_batch\", \"taskIds\": [\"id1\", \"id2\"] }\n```");
        sb.AppendLine("2. TAMAMEN SIFIRLAMA: ```json\n{ \"command\": \"clear_all_tasks\" }\n```");
        sb.AppendLine("3. TARİH ARALIĞI SİLME (ÇOK ÖNEMLİ): Uzun aralıklar için ASLA `delete_tasks_batch` kullanma (token sınırını aşar)! Bunun yerine: ```json\n{ \"command\": \"delete_tasks_date_range\", \"startDate\": \"2026-03-23\", \"endDate\": \"2026-03-27\" }\n```");

        var systemPrompt = sb.ToString();

        // Sohbet geçmişi
        var contentsList = new List<object>();
        if (history != null)
        {
            foreach (var h in history.OrderBy(m => m.SentAt))
                contentsList.Add(new { role = h.Role == "assistant" ? "model" : "user", parts = new[] { new { text = h.Content } } });
        }

        // Mevcut mesaj ve Multimodal içerik (görseller, ses)
        var currentParts = new List<object>();
        currentParts.Add(new { text = userMessage });

        foreach (var file in contextFiles)
        {
            var extension = Path.GetExtension(file.FileName).ToLower();
            string? mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"  => "image/png",
                ".webp" => "image/webp",
                ".webm" => "audio/webm",
                _ => null
            };

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
                { "generationConfig", new { temperature = 0.7, maxOutputTokens = 65536 } }
            };

            if (!string.IsNullOrEmpty(systemPrompt))
                requestBody["system_instruction"] = new { parts = new[] { new { text = systemPrompt } } };

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
