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
