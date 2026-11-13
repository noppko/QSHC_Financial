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
            ], CanHide: false),
        ];

        public static bool IsArea(string? area) => string.Equals(area, AREA, StringComparison.OrdinalIgnoreCase);
    }
}
