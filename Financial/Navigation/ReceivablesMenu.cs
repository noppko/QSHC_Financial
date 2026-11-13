using Financial.Services.Authorization;

namespace Financial.Navigation
{
    /// <summary>
    /// เมนูของ Area ลูกหนี้สิทธิ (Areas/Receivables) — sidebar เปลี่ยนเป็นชุดนี้เมื่ออยู่ใน Area พร้อมลิงก์กลับระบบหลัก
    /// เพิ่มหน้าตามระยะการพัฒนา (docs/design-receivables-tracking.md ข้อ 11)
    /// </summary>
    public static class ReceivablesMenu
    {
        public const string AREA = "Receivables";

        public static readonly IReadOnlyList<AppMenu.Group> Groups =
        [
            new("ar-main", "ลูกหนี้สิทธิ",
            [
                new("ภาพรวมลูกหนี้", "ยอดคงค้างแยก 4 สิทธิ์ อายุหนี้ และผู้จ่าย", "Home", "Index", AppMenu.ICON_DASHBOARD, AppPolicies.FINANCE, Area: AREA),
                new("รายการลูกหนี้", "ค้นหา/กรองลูกหนี้ตามสิทธิ์ OPD/IPD สถานะ อายุหนี้ พร้อมส่งออก Excel", "Receivables", "Index", AppMenu.ICON_LIST, AppPolicies.FINANCE, Area: AREA),
                new("ชุดเรียกเก็บ", "จัดชุดลูกหนี้ที่ส่งเรียกเก็บ ส่งออก Excel (เติม VN/AN ไว้แล้ว) และบันทึกวันที่ส่ง", "Claims", "Index", AppMenu.ICON_REPORT, AppPolicies.FINANCE, Area: AREA),
                new("นำเข้าผลการเรียกเก็บ", "ดาวน์โหลด template, นำเข้า Excel ผลที่หน่วยงานจ่าย, จับคู่และยืนยันรับชำระ", "Imports", "Index", AppMenu.ICON_UPLOAD, AppPolicies.FINANCE, Area: AREA),
            ], CanHide: false),
        ];

        public static bool IsArea(string? area) => string.Equals(area, AREA, StringComparison.OrdinalIgnoreCase);

        /// <summary>สีประจำสิทธิ์ (hex 6 หลัก) — ใช้ไฮไลต์ card/แท็บ/หัวข้อ ให้เห็นว่ากำลังดูสิทธิ์ไหน</summary>
        public static string SchemeColor(string? code) => code?.ToUpperInvariant() switch
        {
            Models.PayorSchemeCodes.NHSO => "#ea580c",  // ส้ม (สีหลักของระบบ)
            Models.PayorSchemeCodes.CSMBS => "#2563eb", // น้ำเงิน
            Models.PayorSchemeCodes.SSO => "#059669",   // เขียว
            Models.PayorSchemeCodes.LGO => "#7c3aed",   // ม่วง
            _ => "#57534e",
        };
    }
}
