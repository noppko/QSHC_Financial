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
        public const string AR_MATCH_RESULT = "AR_MATCH_RESULT";
        public const string AR_IMPORT_STATUS = "AR_IMPORT_STATUS";
        public const string AR_PAYMENT_SOURCE = "AR_PAYMENT_SOURCE";
        public const string AR_CLAIM_BATCH_STATUS = "AR_CLAIM_BATCH_STATUS";

        /// <summary>รหัสเหตุผลการปฏิเสธแยกตามสิทธิ์ เช่น AR_DENY_REASON_NHSO (dropdown ใน Excel template)</summary>
        public static string ArDenyReason(string schemeCode) => $"AR_DENY_REASON_{schemeCode.ToUpperInvariant()}";
    }

    /// <summary>ผลการจับคู่แถวในไฟล์ผลการเรียกเก็บกับลูกหนี้ (ReferenceValues category AR_MATCH_RESULT)</summary>
    public static class ArMatchResultCodes
    {
        public const string MATCHED = "MATCHED";         // คู่ชัด ยอดได้ = ยอดค้าง
        public const string AMOUNT_DIFF = "AMOUNT_DIFF"; // คู่ชัด ยอดต่าง
        public const string DENIED = "DENIED";           // หน่วยงานไม่จ่าย
        public const string SUGGESTED = "SUGGESTED";     // จากขั้น 3 (HN + วันที่ใกล้ + ยอดใกล้) ต้องให้คนยืนยัน
        public const string MULTIPLE = "MULTIPLE";       // พบหลายรายการ — กระจายยอดหรือเลือกเอง
        public const string NOT_FOUND = "NOT_FOUND";     // ไม่พบลูกหนี้
        public const string DUPLICATE = "DUPLICATE";     // เคยบันทึกแล้ว / ซ้ำในไฟล์
        public const string INVALID = "INVALID";         // ข้อมูลไม่ครบ/ผิดรูปแบบ

        public static readonly IReadOnlyList<string> ALL = [MATCHED, AMOUNT_DIFF, DENIED, SUGGESTED, MULTIPLE, NOT_FOUND, DUPLICATE, INVALID];

        /// <summary>ผลที่รวมตอนยืนยันโดยอัตโนมัติ (ไม่ต้องติ๊ก)</summary>
        public static readonly IReadOnlyList<string> AUTO_ACCEPT = [MATCHED, AMOUNT_DIFF, DENIED];

        /// <summary>ผลที่ยอมรับได้ (มีลูกหนี้ปลายทาง)</summary>
        public static readonly IReadOnlyList<string> ACCEPTABLE = [MATCHED, AMOUNT_DIFF, DENIED, SUGGESTED, MULTIPLE];

        public static readonly IReadOnlyDictionary<string, string> DEFAULT_NAMES = new Dictionary<string, string>
        {
            [MATCHED] = "จับคู่ได้", [AMOUNT_DIFF] = "ยอดต่าง", [DENIED] = "ปฏิเสธ", [SUGGESTED] = "แนะนำ (ต้องยืนยัน)",
            [MULTIPLE] = "พบหลายรายการ", [NOT_FOUND] = "ไม่พบ", [DUPLICATE] = "ซ้ำ", [INVALID] = "ข้อมูลไม่ถูกต้อง",
        };
    }

    /// <summary>สถานะไฟล์นำเข้า (ReferenceValues category AR_IMPORT_STATUS)</summary>
    public static class ArImportStatusCodes
    {
        public const string VALIDATED = "VALIDATED";
        public const string CONFIRMED = "CONFIRMED";
        public const string CANCELLED = "CANCELLED";

        public static readonly IReadOnlyDictionary<string, string> DEFAULT_NAMES = new Dictionary<string, string>
        {
            [VALIDATED] = "รอยืนยัน", [CONFIRMED] = "ยืนยันแล้ว", [CANCELLED] = "ยกเลิก",
        };
    }

    /// <summary>สถานะชุดเรียกเก็บ (ReferenceValues category AR_CLAIM_BATCH_STATUS)</summary>
    public static class ArClaimBatchStatusCodes
    {
        public const string DRAFT = "DRAFT";
        public const string SENT = "SENT";
        public const string CANCELLED = "CANCELLED";

        public static readonly IReadOnlyDictionary<string, string> DEFAULT_NAMES = new Dictionary<string, string>
        {
            [DRAFT] = "จัดชุด (ยังไม่ส่ง)", [SENT] = "ส่งแล้ว", [CANCELLED] = "ยกเลิก",
        };
    }

    /// <summary>ที่มาของการรับชำระ (ReferenceValues category AR_PAYMENT_SOURCE)</summary>
    public static class ArPaymentSourceCodes
    {
        public const string IMPORT = "IMPORT";
        public const string MANUAL = "MANUAL";
        public const string WRITE_OFF = "WRITE_OFF";
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
