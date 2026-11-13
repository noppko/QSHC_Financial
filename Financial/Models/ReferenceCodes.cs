namespace Financial.Models
{
    /// <summary>
    /// Category ของตาราง ReferenceValues ที่ระบบใช้ (แทน Enum)
    /// </summary>
    public static class ReferenceCategories
    {
        public const string PATIENT_TYPE = "PATIENT_TYPE";
        public const string ACCESS_LEVEL = "ACCESS_LEVEL";
        public const string PAYOR_SCHEME = "PAYOR_SCHEME";
        public const string FISCAL_ZONE = "FISCAL_ZONE";
        public const string AR_STATUS = "AR_STATUS";
    }

    /// <summary>
    /// รหัสใน ReferenceValues category AR_STATUS — สถานะลูกหนี้สิทธิ (docs/design-receivables-tracking.md ข้อ 4)
    /// </summary>
    public static class ArStatusCodes
    {
        public const string OPEN = "OPEN";           // ตั้งหนี้
        public const string CLAIMED = "CLAIMED";     // ส่งเรียกเก็บแล้ว
        public const string PARTIAL = "PARTIAL";     // ได้รับบางส่วน
        public const string PAID = "PAID";           // ได้รับครบ
        public const string DENIED = "DENIED";       // ถูกปฏิเสธ
        public const string APPEAL = "APPEAL";       // อุทธรณ์/ส่งใหม่
        public const string WRITE_OFF = "WRITE_OFF"; // ตัดหนี้
        public const string CANCELLED = "CANCELLED"; // บิลถูกยกเลิกใน HIS

        /// <summary>สถานะที่ยังมียอดต้องติดตาม (นับในยอดคงค้าง)</summary>
        public static readonly IReadOnlyList<string> OUTSTANDING = [OPEN, CLAIMED, PARTIAL, DENIED, APPEAL];

        public static readonly IReadOnlyList<string> ALL = [OPEN, CLAIMED, PARTIAL, PAID, DENIED, APPEAL, WRITE_OFF, CANCELLED];

        /// <summary>ชื่อสำรองเมื่ออ่าน ReferenceValues ไม่ได้ (ชื่อจริงแก้ได้ที่หน้า ค่าอ้างอิง)</summary>
        public static readonly IReadOnlyDictionary<string, string> DEFAULT_NAMES = new Dictionary<string, string>
        {
            [OPEN] = "ตั้งหนี้", [CLAIMED] = "ส่งเรียกเก็บแล้ว", [PARTIAL] = "ได้รับบางส่วน", [PAID] = "ได้รับครบ",
            [DENIED] = "ถูกปฏิเสธ", [APPEAL] = "อุทธรณ์/ส่งใหม่", [WRITE_OFF] = "ตัดหนี้", [CANCELLED] = "ยกเลิก",
        };
    }

    /// <summary>
    /// รหัสใน ReferenceValues category PATIENT_TYPE (ตรงกับฝั่งบิลใน HIS: O = OPD, I = IPD)
    /// </summary>
    public static class PatientTypeCodes
    {
        public const string OPD = "O";
        public const string IPD = "I";

        /// <summary>แปลงค่าจาก URL/ฟอร์ม (O, I, OPD, IPD) เป็นรหัส — ค่าอื่นใช้ค่าเริ่มต้น</summary>
        public static string Normalize(string? value, string defaultCode = IPD) => value?.Trim().ToUpperInvariant() switch
        {
            "O" or "OPD" => OPD,
            "I" or "IPD" => IPD,
            _ => defaultCode,
        };

        /// <summary>ตัวย่อสำหรับแสดงผล/ชื่อ action เช่น "IPD"</summary>
        public static string Abbreviation(string code) => code == IPD ? "IPD" : "OPD";
    }

    /// <summary>
    /// รหัสใน ReferenceValues category ACCESS_LEVEL
    /// </summary>
    public static class AccessLevelCodes
    {
        public const string ADMIN = "ADMIN";
        public const string FINANCE = "FINANCE";
        public const string GENERAL = "GENERAL";
    }

    /// <summary>
    /// รหัสใน ReferenceValues category PAYOR_SCHEME — สิทธิ์ที่มีรายงานตามปีงบประมาณ
    /// กลุ่มผู้จ่าย (PYRACAT ใน HIS) ของแต่ละสิทธิ์ตั้งค่าได้ที่ Reports:Fiscal:Schemes
    /// </summary>
    public static class PayorSchemeCodes
    {
        public const string NHSO = "NHSO";   // สปสช.
        public const string CSMBS = "CSMBS"; // กรมบัญชีกลาง (สวัสดิการรักษาพยาบาลข้าราชการ)
        public const string SSO = "SSO";     // ประกันสังคม
        public const string LGO = "LGO";     // อปท. (องค์กรปกครองส่วนท้องถิ่น)
    }

    /// <summary>รหัสใน ReferenceValues category FISCAL_ZONE: ในเขต / นอกเขต / รวม</summary>
    public static class FiscalZoneCodes
    {
        public const string IN = "IN";
        public const string OUT = "OUT";
        public const string ALL = "ALL";
    }
}
