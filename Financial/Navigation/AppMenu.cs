using Financial.Services.Authorization;

namespace Financial.Navigation
{
    /// <summary>
    /// รายการเมนูของระบบ — แหล่งเดียวที่ใช้ทั้ง Sidebar และหน้า SiteMap
    /// Policy = null หมายถึงผู้ใช้ที่ Login แล้วทุกคนเห็นได้
    /// </summary>
    public static class AppMenu
    {
        public sealed record Item(string Name, string Description, string Controller, string Action, string IconPath, string? Policy = null);

        /// <param name="Key">รหัสถาวรของกลุ่ม (ใช้เก็บการตั้งค่าเปิด/ปิด ห้ามเปลี่ยน)</param>
        /// <param name="CanHide">false = ซ่อนไม่ได้ (กลุ่มผู้ดูแลระบบ กัน Admin หาหน้าตั้งค่าไม่เจอ)</param>
        public sealed record Group(string Key, string Title, IReadOnlyList<Item> Items, bool CanHide = true);

        // Icon: SVG path (stroke) ขนาด 24x24 — ฝังในหน้าเว็บ ไม่ต้องโหลด icon font จาก Internet
        private const string ICON_DASHBOARD = "M3 13h8V3H3v10Zm0 8h8v-6H3v6Zm10 0h8V11h-8v10Zm0-18v6h8V3h-8Z";
        private const string ICON_BED = "M3 7v11m0-4h18v4M7 10a2 2 0 1 0 0-.01M11 14V9h6a4 4 0 0 1 4 4v1";
        private const string ICON_WALK = "M13 4a1.5 1.5 0 1 0 0-.01M9 21l2.5-6.5L14 17v4m-6-9 3-3.5 3 1.5 2.5 3M11.5 14.5 10 9";
        private const string ICON_UPLOAD = "M12 16V4m0 0-4 4m4-4 4 4M4 16v3a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-3";
        private const string ICON_CARD = "M3 7a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7Zm0 3h18M7 15h3";
        private const string ICON_MAP = "M9 4 3 6v14l6-2 6 2 6-2V4l-6 2-6-2Zm0 0v14m6-12v14";
        private const string ICON_REPORT = "M9 17v-6m4 6V7m4 10v-3M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z";
        private const string ICON_SHIELD = "M12 3 4 6v6c0 5 3.5 8 8 9 4.5-1 8-4 8-9V6l-8-3Z";
        private const string ICON_SETTINGS = "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm7.4-3a7.4 7.4 0 0 0-.1-1.3l2-1.6-2-3.4-2.4 1a7.6 7.6 0 0 0-2.2-1.3L14.3 3h-4.6l-.4 2.4A7.6 7.6 0 0 0 7.1 6.7l-2.4-1-2 3.4 2 1.6a7.4 7.4 0 0 0 0 2.6l-2 1.6 2 3.4 2.4-1a7.6 7.6 0 0 0 2.2 1.3l.4 2.4h4.6l.4-2.4a7.6 7.6 0 0 0 2.2-1.3l2.4 1 2-3.4-2-1.6c.1-.4.1-.9.1-1.3Z";

        public static readonly IReadOnlyList<Group> Groups =
        [
            new("overview", "ภาพรวม",
            [
                new("Dashboard การเงิน", "รายได้ เงินรับ ผู้ป่วยรอชำระเงิน และ 3D Digital Twin", "Home", "Index", ICON_DASHBOARD),
            ]),
            new("discharge", "ผู้ป่วยจำหน่าย",
            [
                new("Discharge IPD", "ผู้ป่วยในที่จำหน่าย พร้อมส่งออก Excel", "Discharges", "IPD", ICON_BED, AppPolicies.FINANCE),
                new("Discharge OPD", "ผู้ป่วยนอก พร้อมส่งออก Excel", "Discharges", "OPD", ICON_WALK, AppPolicies.FINANCE),
            ]),
            new("reports", "รายงาน",
            [
                new("สปสช. ตามปีงบประมาณ", "สิทธิ สปสช. แยก OPD/IPD ในเขต/นอกเขต ตามปีงบประมาณ พร้อมส่งออก Excel", "Reports", "NhsoFiscal", ICON_REPORT, AppPolicies.FINANCE),
            ]),
            new("interface", "Interface",
            [
                new("นำเข้าไฟล์ EDC", "อัปโหลดไฟล์ EDC (.txt) เข้าสู่ระบบ Interface", "InterfaceSystem", "Index", ICON_UPLOAD, AppPolicies.FINANCE),
                new("ข้อมูล EDC", "รายการลงทะเบียน EDC ล่าสุด และตรวจไฟล์ก่อนนำเข้า", "InterfaceSystem", "EDCIndex", ICON_CARD, AppPolicies.FINANCE),
            ]),
            new("admin", "ผู้ดูแลระบบ",
            [
                new("ตั้งค่าระบบ", "สถานะการเชื่อมต่อ การตั้งค่า และสิทธิ์การใช้งาน", "Admin", "Index", ICON_SETTINGS, AppPolicies.ADMIN),
            ], CanHide: false),
            new("other", "อื่นๆ",
            [
                new("แผนผังระบบ", "รายการหน้าที่คุณมีสิทธิ์ใช้งาน", "Home", "SiteMap", ICON_MAP),
                new("ความเป็นส่วนตัว", "นโยบายการใช้ข้อมูล", "Home", "Privacy", ICON_SHIELD),
            ]),
        ];
    }
}
