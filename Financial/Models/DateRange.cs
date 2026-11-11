using System.Globalization;

namespace Financial.Models
{
    /// <summary>
    /// ช่วงวันที่สำหรับค้นหา (รับจาก query string รูปแบบ yyyy-MM-dd ของ input type="date")
    /// </summary>
    public readonly record struct DateRange(DateOnly Start, DateOnly End)
    {
        public const string INPUT_FORMAT = "yyyy-MM-dd";
        public const int MAX_DAYS = 366;

        public int Days => End.DayNumber - Start.DayNumber + 1;

        /// <summary>ค่าส่งให้ Stored Procedure (ค.ศ. เสมอ ไม่ขึ้นกับ culture ของ server)</summary>
        public string SqlFrom => Start.ToString(INPUT_FORMAT, CultureInfo.InvariantCulture) + " 00:00:00.000";
        // .997 เพราะ SQL Server datetime ปัด .999 ขึ้นเป็นเที่ยงคืนของวันถัดไป
        public string SqlTo => End.ToString(INPUT_FORMAT, CultureInfo.InvariantCulture) + " 23:59:59.997";

        public string StartInput => Start.ToString(INPUT_FORMAT, CultureInfo.InvariantCulture);
        public string EndInput => End.ToString(INPUT_FORMAT, CultureInfo.InvariantCulture);

        /// <summary>
        /// แปลงค่าจากฟอร์ม ถ้าไม่ระบุจะใช้ "เมื่อวาน" ตามพฤติกรรมเดิมของระบบ
        /// </summary>
        public static bool TryParse(string? start, string? end, DateOnly today, out DateRange range, out string? error)
        {
            var yesterday = today.AddDays(-1);
            range = new DateRange(yesterday, yesterday);
            error = null;

            if (!TryParseDate(start, yesterday, out var s) || !TryParseDate(end, s, out var e))
            {
                error = "รูปแบบวันที่ไม่ถูกต้อง";
                return false;
            }

            if (e < s)
            {
                error = "วันที่สิ้นสุดต้องไม่น้อยกว่าวันที่เริ่มต้น";
                return false;
            }

            if (e.DayNumber - s.DayNumber + 1 > MAX_DAYS)
            {
                error = $"ค้นหาได้ไม่เกิน {MAX_DAYS} วันต่อครั้ง";
                return false;
            }

            range = new DateRange(s, e);
            return true;
        }

        private static bool TryParseDate(string? value, DateOnly fallback, out DateOnly date)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                date = fallback;
                return true;
            }

            return DateOnly.TryParseExact(value.Trim(), INPUT_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
