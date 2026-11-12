namespace Financial.Models.Dashboard
{
    /// <summary>
    /// ข้อมูลหน้า Dashboard การเงิน (HIS) — ส่งเป็น JSON ให้หน้าเว็บ refresh อัตโนมัติ
    /// นิยามตัวเลขอ้างอิง Database/his/finance.md
    /// </summary>
    public sealed class FinanceDashboard
    {
        public DateTime GeneratedAt { get; init; }

        /// <summary>true = ข้อมูลตัวอย่าง (ยังไม่ได้เชื่อมต่อ HIS จริง)</summary>
        public bool IsSample { get; init; }

        /// <summary>แหล่งข้อมูลที่แสดงบนหน้าจอ</summary>
        public string DataSource { get; init; } = string.Empty;

        /// <summary>ส่วนที่ดึงข้อมูลไม่สำเร็จ (หน้าเว็บยังแสดงส่วนอื่นได้)</summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];

        /// <summary>รายได้ตามวันออกบิล = SUM(PatientBilledItem.NetAmount) ไม่รวมบิลคืนเงิน/Free bill</summary>
        public RevenueSplit RevenueToday { get; init; } = new();
        public RevenueSplit RevenueMonth { get; init; } = new();

        /// <summary>รายได้เดือนก่อน ช่วงวันที่เดียวกับเดือนนี้ (ใช้เทียบ % เปลี่ยนแปลง)</summary>
        public RevenueSplit RevenuePreviousMonthToDate { get; init; } = new();

        /// <summary>บิลเงินสด (CASHBL) เดือนนี้</summary>
        public RevenueSplit CashBilledMonth { get; init; } = new();

        /// <summary>บิลเครดิต (CREDBL) เดือนนี้ = ยอดตั้งลูกหนี้สิทธิ</summary>
        public RevenueSplit CreditBilledMonth { get; init; } = new();
        public int CreditBillsMonth { get; init; }

        /// <summary>รายได้รายวันย้อนหลัง 30 วัน (เรียงจากเก่าไปใหม่)</summary>
        public IReadOnlyList<DailyRevenue> DailyRevenue { get; init; } = [];

        /// <summary>รายได้เดือนนี้แยกตามกลุ่มผู้จ่าย (PayorDetail.PYRACATUID)</summary>
        public IReadOnlyList<PayerRevenue> RevenueByPayer { get; init; } = [];

        /// <summary>เงินรับวันนี้ตามวิธีชำระ (PatientPaymentDetail ตามวันชำระ)</summary>
        public IReadOnlyList<PaymentModeAmount> CollectionsToday { get; init; } = [];

        public PatientVolume Volume { get; init; } = new();

        /// <summary>ผู้ป่วยรอชำระเงิน แยก O/I — ใช้กับ 3D Digital Twin</summary>
        public IReadOnlyList<PendingPayment> PendingPayments { get; init; } = [];
    }

    public sealed class RevenueSplit
    {
        public decimal Opd { get; init; }
        public decimal Ipd { get; init; }
        public decimal Total => Opd + Ipd;
    }

    public sealed record DailyRevenue(DateOnly Date, decimal Opd, decimal Ipd);

    public sealed record PayerRevenue(string Code, string Name, decimal Opd, decimal Ipd)
    {
        public decimal Total => Opd + Ipd;
    }

    public sealed record PaymentModeAmount(string Code, string Name, int Receipts, decimal Received, decimal Refunded, decimal Deposit, decimal NetCollected);

    public sealed class PatientVolume
    {
        public int OpdVisitsToday { get; init; }
        public int IpdAdmittedNow { get; init; }
        public int IpdDischargesToday { get; init; }

        /// <summary>ยอดเฉลี่ยต่อบิลเดือนนี้</summary>
        public decimal AvgBillOpd { get; init; }
        public decimal AvgBillIpd { get; init; }
    }

    /// <summary>
    /// ผู้ป่วยรอชำระเงิน 1 ราย — ผู้ใช้ทั่วไปเห็นเฉพาะรหัสอ้างอิงแบบย่อ (ไม่มี HN/ชื่อ)
    /// Hn / PatientName มีค่าเฉพาะเมื่อผู้ใช้เป็น Admin (บังคับที่ HomeController) ส่วน VisitUid ไม่ถูกส่งออกไปหน้าเว็บเลย
    /// </summary>
    public sealed record PendingPayment(
        string Type,          // "O" = OPD, "I" = IPD
        string Reference,     // รหัสอ้างอิงแบบย่อ เช่น "#4821"
        string PayerCode,
        string PayerName,
        string Location,      // คลินิก (OPD) หรือ หอผู้ป่วย (IPD)
        decimal Amount,       // ค่าใช้จ่ายรวมของ visit
        int WaitingMinutes,   // OPD: นับจาก CHKOUT, IPD: นับจาก Medical Discharge/Discharge Order
        bool InBilling,       // OPD: การเงินรับเรื่องแล้ว (BLINP), IPD: แพทย์ Medical Discharge แล้ว
        [property: System.Text.Json.Serialization.JsonIgnore] long VisitUid = 0,
        string? Hn = null,
        string? PatientName = null,
        string? Zone = null)  // ห้องการเงินใน 3D: PendingZones.OPD / PREMIUM / IPD (null = ตาม Type)
    {
        /// <summary>ห้องการเงินที่ผู้ป่วยไปรอ — Premium Clinic แยกห้องจากคลินิก OPD อื่น</summary>
        public string CounterZone => Zone ?? Type;
    }

    /// <summary>รหัสห้องการเงินใน 3D Digital Twin</summary>
    public static class PendingZones
    {
        public const string OPD = "O";
        public const string PREMIUM = "P";
        public const string IPD = "I";
        /// <summary>รหัสคลินิก (Location.Code) ของ Premium Clinic — ปรับได้ที่ Dashboard:PremiumClinicCodes</summary>
        public static readonly string[] DEFAULT_PREMIUM_CLINIC_CODES = ["D2305"];
        /// <summary>คำในชื่อคลินิก (Location.Name) ที่ถือเป็น Premium Clinic — กันกรณีรหัสใน HIS ไม่ตรงกับที่ตั้งไว้</summary>
        public const string PREMIUM_NAME_KEYWORD = "Premium";

        /// <summary>คลินิกนี้ใช้ห้องการเงิน Premium หรือไม่ (ตรงรหัส หรือชื่อมีคำว่า Premium)</summary>
        public static bool IsPremium(string? locationCode, string? locationName, IReadOnlySet<string>? premiumCodes)
            => (locationCode is not null && premiumCodes is not null && premiumCodes.Contains(locationCode.Trim()))
               || (locationName?.Contains(PREMIUM_NAME_KEYWORD, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
