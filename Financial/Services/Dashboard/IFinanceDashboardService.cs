using Financial.Models.Dashboard;

namespace Financial.Services.Dashboard
{
    public interface IFinanceDashboardService
    {
        /// <param name="includePatientIdentity">true = ใส่ HN และชื่อ-สกุลในรายการผู้ป่วยรอชำระ — ใช้เฉพาะผู้ใช้ Admin เท่านั้น</param>
        Task<FinanceDashboard> GetAsync(bool includePatientIdentity = false, CancellationToken cancellationToken = default);
    }

    public static class DashboardText
    {
        /// <summary>รหัสอ้างอิงแบบย่อ (4 หลักท้ายของ VisitUid) — ไม่ใช่ HN และย้อนกลับเป็นตัวผู้ป่วยไม่ได้จากหน้าจอ</summary>
        public static string Reference(long visitUid) => "#" + (Math.Abs(visitUid) % 10000).ToString("D4");

        /// <summary>ชื่อวิธีชำระ (ReferenceValue โดเมน PAYMD)</summary>
        public static string PaymentModeName(string? code) => code switch
        {
            "CASHH" => "เงินสด",
            "QRCODE" => "QR Code",
            "CARDD" => "บัตรเครดิต/เดบิต",
            "TRNFR" => "โอนเงิน",
            "PATIN" => "Pay in",
            "CHEQU" => "เช็ค",
            _ => string.IsNullOrWhiteSpace(code) || code == "?" ? "ไม่ระบุ" : code,
        };
    }

    /// <summary>
    /// เวลาปัจจุบันของเครื่อง (= DateTime.Now, server ตั้งเวลาไทย) — ห้ามใช้ UtcNow
    /// ผ่าน TimeProvider เพื่อให้ test กำหนดเวลาได้
    /// </summary>
    public static class ThaiClock
    {
        public static DateTime Now(TimeProvider timeProvider) => timeProvider.GetLocalNow().DateTime;
    }
}
