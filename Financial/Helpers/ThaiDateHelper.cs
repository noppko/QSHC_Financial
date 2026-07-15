using System.Globalization;

namespace Financial.Helpers
{
    /// <summary>
    /// Helper สำหรับจัดการวันที่แบบไทย (พ.ศ.)
    /// </summary>
    public static class ThaiDateHelper
    {
        private static readonly CultureInfo ThaiCulture;
        private static readonly CultureInfo GregorianCulture;

        static ThaiDateHelper()
        {
            // สร้าง CultureInfo สำหรับแสดงผลวันที่ไทย (พ.ศ.)
            ThaiCulture = new CultureInfo("th-TH");
            ThaiCulture.DateTimeFormat.Calendar = new ThaiBuddhistCalendar();

            // สร้าง CultureInfo สำหรับ parse/serialize (ค.ศ.)
            GregorianCulture = new CultureInfo("th-TH");
            GregorianCulture.DateTimeFormat.Calendar = new GregorianCalendar();
        }

        /// <summary>
        /// แปลง DateTime เป็นข้อความวันที่แบบไทย (พ.ศ.)
        /// เช่น "7 กรกฎาคม 2569"
        /// </summary>
        public static string ToThaiDateString(this DateTime date, string format = "d MMMM yyyy")
        {
            return date.ToString(format, ThaiCulture);
        }

        /// <summary>
        /// แปลง DateTime เป็นข้อความวันที่และเวลาแบบไทย (พ.ศ.)
        /// เช่น "7 กรกฎาคม 2569 14:30"
        /// </summary>
        public static string ToThaiDateTimeString(this DateTime date, string format = "d MMMM yyyy HH:mm")
        {
            return date.ToString(format, ThaiCulture);
        }

        /// <summary>
        /// แปลง DateTime เป็นข้อความวันที่สั้นแบบไทย (พ.ศ.)
        /// เช่น "7 ก.ค. 69"
        /// </summary>
        public static string ToThaiShortDateString(this DateTime date)
        {
            return date.ToString("d MMM yy", ThaiCulture);
        }

        /// <summary>
        /// Parse วันที่จากข้อความเป็น DateTime (รับค่าเป็นปี ค.ศ.)
        /// ใช้สำหรับ parse ข้อมูลจากฟอร์ม
        /// </summary>
        public static DateTime? ParseThaiDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return null;
            }

            // พยายาม parse ด้วย Gregorian calendar (ค.ศ.)
            if (DateTime.TryParse(dateString, GregorianCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// แปลงปี พ.ศ. เป็น ค.ศ.
        /// </summary>
        public static int BuddhistYearToChristianYear(int buddhistYear)
        {
            return buddhistYear - 543;
        }

        /// <summary>
        /// แปลงปี ค.ศ. เป็น พ.ศ.
        /// </summary>
        public static int ChristianYearToBuddhistYear(int christianYear)
        {
            return christianYear + 543;
        }

        /// <summary>
        /// สร้างวันที่เริ่มต้นของวัน (00:00:00)
        /// </summary>
        public static DateTime StartOfDay(this DateTime date)
        {
            return date.Date;
        }

        /// <summary>
        /// สร้างวันที่สิ้นสุดของวัน (23:59:59.999)
        /// </summary>
        public static DateTime EndOfDay(this DateTime date)
        {
            return date.Date.AddDays(1).AddMilliseconds(-1);
        }

        /// <summary>
        /// ดึงชื่อเดือนภาษาไทย
        /// </summary>
        public static string GetThaiMonthName(int month, bool shortName = false)
        {
            var monthNames = new[]
            {
                "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
                "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"
            };

            var shortMonthNames = new[]
            {
                "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.",
                "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."
            };

            if (month < 1 || month > 12)
            {
                return string.Empty;
            }

            return shortName ? shortMonthNames[month - 1] : monthNames[month - 1];
        }
    }
}
