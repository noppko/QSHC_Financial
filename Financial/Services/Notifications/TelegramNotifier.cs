using Financial.Services.Dashboard;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Financial.Services.Notifications
{
    /// <summary>
    /// ตั้งค่าการแจ้งเตือน Telegram (appsettings.json → Telegram)
    /// BotToken / ChatId เป็นความลับ: ห้ามใส่ใน appsettings.json (อยู่ใน git) — ใช้ User Secrets หรือ environment variable
    ///   dotnet user-secrets set "Telegram:BotToken" "..." / "Telegram:ChatId" "..."   หรือ   Telegram__BotToken, Telegram__ChatId
    /// </summary>
    public class TelegramOptions
    {
        public const string SECTION_NAME = "Telegram";
        public const string DEFAULT_API_BASE_URL = "https://api.telegram.org";

        public bool Enabled { get; set; }
        public string BotToken { get; set; } = string.Empty;

        /// <summary>แชทหรือกลุ่มที่รับข้อความ (กลุ่มเป็นเลขติดลบ) หรือ @channelname</summary>
        public string ChatId { get; set; } = string.Empty;

        public string ApiBaseUrl { get; set; } = DEFAULT_API_BASE_URL;

        /// <summary>proxy ของโรงพยาบาล เช่น http://10.0.0.1:8080 (เซิร์ฟเวอร์ใน intranet ต้องออกอินเทอร์เน็ตผ่าน proxy)</summary>
        public string? Proxy { get; set; }

        public int TimeoutSeconds { get; set; } = 10;

        /// <summary>เรื่องเดียวกันแจ้งซ้ำได้ไม่ถี่กว่านี้ (นาที) — กันข้อความท่วมเมื่อ HIS ล่มต่อเนื่อง</summary>
        public int CooldownMinutes { get; set; } = 30;

        /// <summary>Worker ดึงข้อมูลรายงานปีงบ: แจ้งเมื่อสำเร็จด้วย (ไม่สำเร็จแจ้งเสมอ)</summary>
        public bool NotifyRefreshSuccess { get; set; } = true;

        public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);

        /// <summary>แสดง chat id แบบปิดบังบางส่วน (ใช้ในหน้าตั้งค่า — ไม่แสดง token เด็ดขาด)</summary>
        public string MaskedChatId => ChatId.Length <= 4 ? new string('•', ChatId.Length) : new string('•', ChatId.Length - 4) + ChatId[^4..];
    }

    /// <param name="Sent">ส่งสำเร็จ</param>
    /// <param name="Reason">เหตุผลภาษาไทยเมื่อไม่ได้ส่ง/ส่งไม่สำเร็จ (ไม่มี token หรือข้อมูลผู้ป่วย)</param>
    public sealed record TelegramSendResult(bool Sent, string? Reason = null);

    public interface ITelegramNotifier
    {
        bool IsConfigured { get; }

        /// <summary>
        /// ส่งข้อความ — ไม่ throw (ส่งไม่ได้ไม่ให้กระทบงานหลัก)
        /// ข้อความต้องไม่มี HN/ชื่อ/ข้อมูลผู้ป่วย และไม่มี connection string
        /// </summary>
        /// <param name="dedupeKey">ไม่ null = เรื่องเดียวกันส่งซ้ำไม่ถี่กว่า CooldownMinutes</param>
        Task<TelegramSendResult> SendAsync(string message, string? dedupeKey = null, CancellationToken cancellationToken = default);
    }

    /// <summary>จำเวลาที่แจ้งเรื่องหนึ่งๆ ล่าสุด (singleton ใช้ร่วมกันทั้งแอป)</summary>
    public sealed class AlertCooldown
    {
        private readonly ConcurrentDictionary<string, DateTime> _last = new();

        /// <returns>true = ผ่าน (แจ้งได้ และบันทึกเวลาไว้), false = ยังอยู่ในช่วงพัก</returns>
        public bool TryAcquire(string key, TimeSpan cooldown, DateTime now)
        {
            while (true)
            {
                if (!_last.TryGetValue(key, out var last))
                {
                    if (_last.TryAdd(key, now)) return true;
                    continue;
                }
                if (now - last < cooldown) return false;
                if (_last.TryUpdate(key, now, last)) return true;
            }
        }
    }

    public class TelegramNotifier : ITelegramNotifier
    {
        private const int MAX_TEXT_LENGTH = 3500; // Telegram จำกัด 4096 ตัวอักษร

        private readonly HttpClient _http;
        private readonly TelegramOptions _options;
        private readonly AlertCooldown _cooldown;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TelegramNotifier> _logger;

        public TelegramNotifier(HttpClient http, IOptions<TelegramOptions> options, AlertCooldown cooldown, TimeProvider timeProvider, ILogger<TelegramNotifier> logger)
        {
            _http = http;
            _options = options.Value;
            _cooldown = cooldown;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;

        public async Task<TelegramSendResult> SendAsync(string message, string? dedupeKey = null, CancellationToken cancellationToken = default)
        {
            if (!_options.IsConfigured) return new TelegramSendResult(false, "ยังไม่ได้ตั้งค่า Telegram (Enabled / BotToken / ChatId)");

            var now = ThaiClock.Now(_timeProvider);
            if (dedupeKey is not null && !_cooldown.TryAcquire(dedupeKey, TimeSpan.FromMinutes(Math.Max(0, _options.CooldownMinutes)), now))
            {
                return new TelegramSendResult(false, "แจ้งเรื่องนี้ไปแล้วเมื่อสักครู่ (อยู่ในช่วงพัก)");
            }

            var text = $"[QSHC Financial] {message}\n{now.ToString("d MMM yyyy HH:mm", CultureInfo.GetCultureInfo("th-TH"))} น.";
            if (text.Length > MAX_TEXT_LENGTH) text = text[..MAX_TEXT_LENGTH] + "…";

            try
            {
                // token อยู่ใน URL ตามรูปแบบของ Telegram — ห้าม log URL (ลงทะเบียน HttpClient โดยเอา logger ออกแล้ว)
                var url = $"{_options.ApiBaseUrl.TrimEnd('/')}/bot{_options.BotToken}/sendMessage";
                using var response = await _http.PostAsJsonAsync(url, new { chat_id = _options.ChatId, text, disable_web_page_preview = true }, cancellationToken);
                if (response.IsSuccessStatusCode) return new TelegramSendResult(true);

                var description = await ReadDescriptionAsync(response, cancellationToken);
                _logger.LogWarning("Telegram alert rejected: HTTP {Status} {Description}", (int)response.StatusCode, description);
                return new TelegramSendResult(false, $"Telegram ปฏิเสธ (HTTP {(int)response.StatusCode}{(description is null ? "" : $": {description}")})");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                // ไม่ log ex เต็ม: ข้อความ exception ของ HttpRequestException อาจมี URL ที่มี token
                _logger.LogWarning("Telegram alert failed: {ExceptionType}", ex.GetType().Name);
                return new TelegramSendResult(false, "เชื่อมต่อ Telegram ไม่ได้ — ตรวจว่าเซิร์ฟเวอร์ออกอินเทอร์เน็ตได้ หรือตั้งค่า Telegram:Proxy");
            }
        }

        private static async Task<string?> ReadDescriptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                return doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
