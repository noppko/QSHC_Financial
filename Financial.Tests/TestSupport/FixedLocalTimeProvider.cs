namespace Financial.Tests.TestSupport
{
    /// <summary>
    /// TimeProvider ที่ตรึงเวลาไทยไว้ — test ระบุเวลาไทยตรงๆ (แอปใช้ GetLocalNow() = DateTime.Now)
    /// หมายเหตุ: TimeProvider ของ .NET ให้ override ได้เฉพาะ GetUtcNow จึงแปลงกลับภายใน helper นี้ที่เดียว
    /// </summary>
    public sealed class FixedLocalTimeProvider(DateTime localNow) : TimeProvider
    {
        private static readonly TimeZoneInfo Thailand = TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");

        public override TimeZoneInfo LocalTimeZone => Thailand;

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(localNow, TimeSpan.FromHours(7)).ToOffset(TimeSpan.Zero);
    }
}
