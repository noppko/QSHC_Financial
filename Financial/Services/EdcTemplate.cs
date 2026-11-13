namespace Financial.Services
{
    /// <summary>
    /// ไฟล์ตัวอย่าง (template) สำหรับนำเข้า EDC — 1 บรรทัด = 1 รายการ คั่นด้วย |
    /// ลำดับช่องอิงตามตาราง PatientEDCRegister ใน InterfaceSystem; ข้อมูลเป็นค่าสมมติทั้งหมด (ไม่มีข้อมูลผู้ป่วยจริง)
    /// </summary>
    public static class EdcTemplate
    {
        public const string FILE_NAME = "HCG14584_TEMPLATE.txt";
        public const char SEPARATOR = '|';

        /// <summary>ชื่อช่องตามลำดับในไฟล์ (ใช้แสดงในหน้านำเข้าด้วย)</summary>
        public static readonly IReadOnlyList<string> FIELDS =
        [
            "Code", "CompanyCode", "CompanyName", "MerchantNo", "MerchantName", "Htype", "TerminalID",
            "TransactionDate", "PostDate", "PersonalID", "Name", "Surname", "RefPID", "PIDCus", "CusName", "CusSurname",
            "PAIDType1", "PAIDType2", "BirthDate", "EditDate", "TranAMT", "Batch", "Trace", "TxnsCode", "AppCode", "TransRefID",
        ];

        private static readonly string[] SAMPLE_ROW =
        [
            "EDC01", "00000001", "ตัวอย่าง บริษัท", "0000000001", "ตัวอย่าง ร้านค้า", "H", "T000000001",
            "20260101120000", "20260102", "0000000000000", "ตัวอย่าง", "ทดสอบ", "", "", "", "",
            "CASH", "", "19900101", "", "100.00", "000001", "000001", "SALE", "APP000", "REF0000001",
        ];

        public static string Build() => string.Join(SEPARATOR, SAMPLE_ROW) + "\r\n";
    }
}
