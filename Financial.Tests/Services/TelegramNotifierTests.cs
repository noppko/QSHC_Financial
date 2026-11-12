using Financial.Services.Notifications;
using Financial.Services.Reports;
using Financial.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Financial.Tests.Services
{
    public class TelegramNotifierTests
    {
        private const string TOKEN = "123456:SECRET-TOKEN";
        private static readonly DateTime NOW = new(2026, 11, 12, 13, 30, 0);

        private sealed class FakeHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "{\"ok\":true}", Exception? throws = null) : HttpMessageHandler
        {
            public List<(Uri? Uri, string Body)> Requests { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add((request.RequestUri, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
                if (throws is not null) throw throws;
                return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class CapturingLogger : ILogger<TelegramNotifier>
        {
            public List<string> Lines { get; } = [];
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Lines.Add(formatter(state, exception) + (exception?.ToString() ?? ""));
        }

        private static (TelegramNotifier Notifier, FakeHandler Handler, CapturingLogger Log) Create(FakeHandler? handler = null, Action<TelegramOptions>? configure = null, AlertCooldown? cooldown = null)
        {
            var options = new TelegramOptions { Enabled = true, BotToken = TOKEN, ChatId = "-1001234", CooldownMinutes = 30 };
            configure?.Invoke(options);
            handler ??= new FakeHandler();
            var log = new CapturingLogger();
            var notifier = new TelegramNotifier(new HttpClient(handler), Options.Create(options), cooldown ?? new AlertCooldown(), new FixedLocalTimeProvider(NOW), log);
            return (notifier, handler, log);
        }

        [Fact]
        public async Task Send_PostsToBotApi_WithChatIdAndThaiTimestamp()
        {
            var (notifier, handler, _) = Create();

            var result = await notifier.SendAsync("ทดสอบ");

            result.Sent.Should().BeTrue();
            var request = handler.Requests.Single();
            request.Uri!.ToString().Should().Be($"https://api.telegram.org/bot{TOKEN}/sendMessage");
            using var json = JsonDocument.Parse(request.Body);
            json.RootElement.GetProperty("chat_id").GetString().Should().Be("-1001234");
            var text = json.RootElement.GetProperty("text").GetString()!;
            text.Should().StartWith("[QSHC Financial] ทดสอบ").And.EndWith("น.").And.Contain("13:30");
        }

        [Theory]
        [InlineData(false, "tok", "chat")]
        [InlineData(true, "", "chat")]
        [InlineData(true, "tok", "")]
        public async Task Send_NotConfigured_DoesNothing(bool enabled, string token, string chat)
        {
            var (notifier, handler, _) = Create(configure: o => { o.Enabled = enabled; o.BotToken = token; o.ChatId = chat; });

            var result = await notifier.SendAsync("x");

            result.Sent.Should().BeFalse();
            result.Reason.Should().Contain("ยังไม่ได้ตั้งค่า");
            notifier.IsConfigured.Should().BeFalse();
            handler.Requests.Should().BeEmpty("ไม่ออกเครือข่ายเมื่อไม่ได้ตั้งค่า");
        }

        [Fact]
        public async Task Send_SameDedupeKey_IsSentOncePerCooldown()
        {
            var cooldown = new AlertCooldown();
            var (notifier, handler, _) = Create(cooldown: cooldown);

            (await notifier.SendAsync("a", "his")).Sent.Should().BeTrue();
            var second = await notifier.SendAsync("a", "his");
            (await notifier.SendAsync("b", "other")).Sent.Should().BeTrue("คนละเรื่องไม่ถูกกัน");
            (await notifier.SendAsync("c")).Sent.Should().BeTrue("ไม่มี dedupeKey = ส่งเสมอ");

            second.Sent.Should().BeFalse();
            second.Reason.Should().Contain("ช่วงพัก");
            handler.Requests.Should().HaveCount(3);
        }

        [Fact]
        public void Cooldown_AllowsAgainAfterWindow()
        {
            var cooldown = new AlertCooldown();
            var start = NOW;

            cooldown.TryAcquire("k", TimeSpan.FromMinutes(30), start).Should().BeTrue();
            cooldown.TryAcquire("k", TimeSpan.FromMinutes(30), start.AddMinutes(29)).Should().BeFalse();
            cooldown.TryAcquire("k", TimeSpan.FromMinutes(30), start.AddMinutes(31)).Should().BeTrue();
        }

        [Fact]
        public async Task Send_HttpError_ReturnsReason_WithoutThrowing_AndNeverLogsToken()
        {
            var (notifier, _, log) = Create(new FakeHandler(HttpStatusCode.Unauthorized, "{\"ok\":false,\"description\":\"Unauthorized\"}"));

            var result = await notifier.SendAsync("x");

            result.Sent.Should().BeFalse();
            result.Reason.Should().Contain("401").And.Contain("Unauthorized");
            result.Reason.Should().NotContain(TOKEN);
            log.Lines.Should().NotContain(l => l.Contains("SECRET-TOKEN"));
        }

        [Fact]
        public async Task Send_NetworkFailure_IsSwallowed_AndTokenNotInLogsOrReason()
        {
            // HttpRequestException ของจริงมักมี URL (ซึ่งมี token) ในข้อความ
            var (notifier, _, log) = Create(new FakeHandler(throws: new HttpRequestException($"Name or service not known (api.telegram.org:443) /bot{TOKEN}/sendMessage")));

            var result = await notifier.SendAsync("x");

            result.Sent.Should().BeFalse();
            result.Reason.Should().Contain("เชื่อมต่อ Telegram ไม่ได้").And.NotContain("SECRET");
            log.Lines.Should().NotContain(l => l.Contains("SECRET-TOKEN"));
        }

        [Fact]
        public async Task Send_TruncatesVeryLongMessage()
        {
            var (notifier, handler, _) = Create();

            await notifier.SendAsync(new string('ก', 6000));

            using var json = JsonDocument.Parse(handler.Requests.Single().Body);
            json.RootElement.GetProperty("text").GetString()!.Length.Should().BeLessThanOrEqualTo(3501);
        }

        [Fact]
        public void Options_MaskChatId_AndDefaults()
        {
            new TelegramOptions { ChatId = "-1001234567" }.MaskedChatId.Should().Be("•••••••4567", "แสดงเฉพาะ 4 ตัวท้าย");
            new TelegramOptions().NotifyRefreshSuccess.Should().BeTrue("Worker แจ้งผลสำเร็จด้วยตามที่ผู้ใช้ขอ");
            new TelegramOptions().Enabled.Should().BeFalse();
        }

        [Fact]
        public void WorkerMessages_ContainYearAndOutcome_ButNoExceptionMessage()
        {
            RefreshMessages.Done(2569, 33_098, TimeSpan.FromSeconds(247)).Should().Contain("2569").And.Contain("33,098").And.Contain("4 นาที 7 วินาที").And.Contain("สำเร็จ");
            var failed = RefreshMessages.Failed(2569, "SqlException");
            failed.Should().Contain("2569").And.Contain("SqlException").And.Contain("ไม่สำเร็จ");
        }
    }
}
