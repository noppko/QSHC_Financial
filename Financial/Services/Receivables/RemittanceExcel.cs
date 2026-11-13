using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Globalization;

namespace Financial.Services.Receivables
{
    /// <summary>1 แถวที่อ่านจากไฟล์ผลการเรียกเก็บ — Error ไม่ว่าง = แถวผิดรูปแบบ (INVALID)</summary>
    public sealed record RemittanceRow(int RowNo, string? VisitNumber, string? Hn, DateTime? VisitDate, DateTime? DischargeDate, string? BillNumber,
        decimal? ClaimedAmount, decimal ReceivedAmount, decimal DeniedAmount, string? ReasonCode, string? ReasonText, string? StatementNo,
        DateTime? TransferDate, string? Note, string? Error = null);

    /// <param name="SchemeInFile">รหัสสิทธิ์ที่ฝังใน template (sheet ซ่อน) — null ถ้าไม่ใช่ไฟล์จาก template</param>
    /// <param name="Errors">ปัญหาระดับไฟล์ (ไม่มีหัวคอลัมน์ที่บังคับ, แถวเกินกำหนด) — มีแล้วนำเข้าไม่ได้</param>
    /// <param name="ClaimBatchNo">เลขชุดเรียกเก็บ ถ้าไฟล์ส่งออกจากชุดเรียกเก็บ</param>
    public sealed record RemittanceParseResult(string? SchemeInFile, IReadOnlyList<RemittanceRow> Rows, IReadOnlyList<string> Errors, string? ClaimBatchNo = null);

    /// <summary>แถวที่เติมไว้ล่วงหน้าใน template จากชุดเรียกเก็บ (หน่วยงาน/เจ้าหน้าที่กรอกยอดที่ได้รับกลับมา)</summary>
    /// <param name="Hn">null = ไม่ใส่ HN (ผู้ส่งออกไม่ใช่ Admin)</param>
    public sealed record ClaimTemplateRow(string? VisitNumber, string? Hn, DateTime? VisitDate, DateTime? DischargeDate, string BillNumber, decimal ClaimedAmount);

    /// <summary>
    /// Excel template กลางสำหรับผลการเรียกเก็บ (แยกไฟล์ต่อสิทธิ์) และตัวอ่านไฟล์ — docs/design-receivables-tracking.md ข้อ 6
    /// ไม่มีข้อมูลผู้ป่วยจริงใน template (ตัวอย่างเป็นค่าสมมติใน sheet คำอธิบาย)
    /// </summary>
    public static class RemittanceExcel
    {
        public const string SHEET_DATA = "ผลการเรียกเก็บ";
        public const string SHEET_HELP = "คำอธิบาย";
        public const string SHEET_CODES = "ค่าอ้างอิง";
        public const string SHEET_META = "_meta";
        public const string META_SCHEME_KEY = "SCHEME";
        public const string META_CLAIM_BATCH_KEY = "CLAIM_BATCH";
        public const int BUDDHIST_YEAR_THRESHOLD = 2400;

        public const string COL_NO = "ลำดับ";
        public const string COL_VN = "VN/AN";
        public const string COL_HN = "HN";
        public const string COL_VISIT_DATE = "วันที่เปิด visit";
        public const string COL_DISCHARGE_DATE = "วันที่จำหน่าย";
        public const string COL_BILL = "เลขที่บิล";
        public const string COL_CLAIMED = "ยอดเรียกเก็บ";
        public const string COL_RECEIVED = "ยอดที่ได้รับ";
        public const string COL_DENIED = "ยอดปฏิเสธ/หัก";
        public const string COL_REASON_CODE = "รหัสเหตุผล";
        public const string COL_REASON_TEXT = "รายละเอียดเหตุผล";
        public const string COL_STATEMENT = "เลขที่ statement/REP";
        public const string COL_TRANSFER_DATE = "วันที่โอนเงิน";
        public const string COL_NOTE = "หมายเหตุ";

        public static readonly IReadOnlyList<string> COLUMNS =
        [
            COL_NO, COL_VN, COL_HN, COL_VISIT_DATE, COL_DISCHARGE_DATE, COL_BILL, COL_CLAIMED, COL_RECEIVED, COL_DENIED,
            COL_REASON_CODE, COL_REASON_TEXT, COL_STATEMENT, COL_TRANSFER_DATE, COL_NOTE,
        ];

        private static readonly string[] TEXT_COLUMNS = [COL_VN, COL_HN, COL_BILL, COL_REASON_CODE, COL_STATEMENT];
        private static readonly string[] DATE_COLUMNS = [COL_VISIT_DATE, COL_DISCHARGE_DATE, COL_TRANSFER_DATE];
        private static readonly string[] AMOUNT_COLUMNS = [COL_CLAIMED, COL_RECEIVED, COL_DENIED];
        private static readonly string[] DATE_FORMATS = ["d/M/yyyy", "dd/MM/yyyy", "d/M/yyyy H:mm", "d/M/yyyy H:mm:ss", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "d-M-yyyy"];

        static RemittanceExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public static string FileName(string schemeCode) => $"AR_REMITTANCE_{schemeCode.ToUpperInvariant()}.xlsx";

        /// <param name="reasons">รหัสเหตุผลการปฏิเสธของสิทธิ์นี้ (Code, Name) — ว่างได้</param>
        /// <param name="claimBatchNo">ส่งออกจากชุดเรียกเก็บ — ฝังเลขชุดใน sheet ซ่อน</param>
        /// <param name="prefill">แถวที่เติมไว้ (VN/AN, HN, วันที่, เลขบิล, ยอดเรียกเก็บ) — เหลือกรอกยอดที่ได้รับ</param>
        public static byte[] CreateTemplate(string schemeCode, string schemeName, IReadOnlyList<(string Code, string Name)> reasons,
            string? claimBatchNo = null, IReadOnlyList<ClaimTemplateRow>? prefill = null)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add(SHEET_DATA);
            if (prefill is not null)
            {
                for (var i = 0; i < prefill.Count; i++)
                {
                    var p = prefill[i];
                    var row = i + 2;
                    ws.Cells[row, ColumnIndex(COL_NO)].Value = i + 1;
                    ws.Cells[row, ColumnIndex(COL_VN)].Value = p.VisitNumber;
                    ws.Cells[row, ColumnIndex(COL_HN)].Value = p.Hn;
                    ws.Cells[row, ColumnIndex(COL_VISIT_DATE)].Value = p.VisitDate;
                    ws.Cells[row, ColumnIndex(COL_DISCHARGE_DATE)].Value = p.DischargeDate;
                    ws.Cells[row, ColumnIndex(COL_BILL)].Value = p.BillNumber;
                    ws.Cells[row, ColumnIndex(COL_CLAIMED)].Value = p.ClaimedAmount;
                }
            }
            for (var c = 0; c < COLUMNS.Count; c++)
            {
                var name = COLUMNS[c];
                var col = c + 1;
                ws.Cells[1, col].Value = name;
                var column = ws.Cells[2, col, 20_001, col];
                if (TEXT_COLUMNS.Contains(name)) column.Style.Numberformat.Format = "@"; // กันเลข 0 หน้า HN/VN หาย
                else if (DATE_COLUMNS.Contains(name)) column.Style.Numberformat.Format = "dd/mm/yyyy";
                else if (AMOUNT_COLUMNS.Contains(name)) column.Style.Numberformat.Format = "#,##0.00";
            }
            var header = ws.Cells[1, 1, 1, COLUMNS.Count];
            header.Style.Font.Bold = true;
            header.Style.Fill.PatternType = ExcelFillStyle.Solid;
            header.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(221, 235, 247));
            foreach (var required in new[] { COL_VN, COL_HN, COL_VISIT_DATE, COL_RECEIVED })
            {
                ws.Cells[1, ColumnIndex(required)].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 230, 204));
            }
            ws.View.FreezePanes(2, 1);
            for (var c = 1; c <= COLUMNS.Count; c++) ws.Column(c).Width = c is 11 or 14 ? 36 : 18;

            // sheet ค่าอ้างอิง + dropdown รหัสเหตุผล
            var codes = package.Workbook.Worksheets.Add(SHEET_CODES);
            codes.Cells[1, 1].Value = "รหัสเหตุผล";
            codes.Cells[1, 2].Value = "ความหมาย";
            codes.Cells[1, 1, 1, 2].Style.Font.Bold = true;
            for (var i = 0; i < reasons.Count; i++)
            {
                codes.Cells[i + 2, 1].Value = reasons[i].Code;
                codes.Cells[i + 2, 2].Value = reasons[i].Name;
            }
            if (reasons.Count == 0) codes.Cells[2, 1].Value = $"(ยังไม่ได้ตั้งรหัสเหตุผลของ {schemeName} — ตั้งได้ที่หน้า ค่าอ้างอิง หมวด {Models.ReferenceCategories.ArDenyReason(schemeCode)})";
            else
            {
                var col = ColumnIndex(COL_REASON_CODE);
                var validation = ws.DataValidations.AddListValidation(ws.Cells[2, col, 20_001, col].Address);
                validation.Formula.ExcelFormula = $"'{SHEET_CODES}'!$A$2:$A${reasons.Count + 1}";
                validation.ShowErrorMessage = false; // รหัสใหม่ที่ยังไม่อยู่ในรายการ พิมพ์เองได้
            }
            codes.Column(1).Width = 18;
            codes.Column(2).Width = 60;

            // sheet คำอธิบาย (ตัวอย่างเป็นข้อมูลสมมติ)
            var help = package.Workbook.Worksheets.Add(SHEET_HELP);
            string[] lines =
            [
                $"ผลการเรียกเก็บ — สิทธิ์ {schemeName} ({schemeCode})",
                "",
                "วิธีกรอก",
                $"1. กรอกใน sheet \"{SHEET_DATA}\" ตั้งแต่แถว 2 — ห้ามแก้ชื่อหัวคอลัมน์ (สลับลำดับคอลัมน์ได้)",
                $"2. ต้องมี {COL_VN} หรือ {COL_HN} + {COL_VISIT_DATE} อย่างน้อยหนึ่งชุด และต้องมี {COL_RECEIVED} (0 = ไม่จ่าย)",
                "3. วันที่ใช้รูปแบบ วัน/เดือน/ปี รับทั้ง พ.ศ. และ ค.ศ. (ปีมากกว่า 2400 ถือเป็น พ.ศ.)",
                $"4. ผู้ป่วยใน: {COL_VISIT_DATE} = วันที่ admit · ถ้า 1 visit มีหลายบิล ใส่ {COL_BILL} ช่วยแยก",
                $"5. {COL_STATEMENT} / {COL_TRANSFER_DATE} เว้นได้ ระบบใช้ค่าที่กรอกตอนนำเข้า",
                "6. ไฟล์ .xlsx ไม่เกิน 20,000 แถว — ไฟล์นี้ใช้กับสิทธิ์นี้เท่านั้น (นำเข้าผิดสิทธิ์ระบบจะไม่รับ)",
                "",
                "ตัวอย่าง (ข้อมูลสมมติ)",
            ];
            for (var i = 0; i < lines.Length; i++) help.Cells[i + 1, 1].Value = lines[i];
            help.Cells[1, 1].Style.Font.Bold = true;
            help.Cells[1, 1].Style.Font.Size = 14;
            var sampleRow = lines.Length + 1;
            for (var c = 0; c < COLUMNS.Count; c++) help.Cells[sampleRow, c + 1].Value = COLUMNS[c];
            help.Cells[sampleRow, 1, sampleRow, COLUMNS.Count].Style.Font.Bold = true;
            object?[][] samples =
            [
                [1, "AN69000001", "00000001", "01/10/2569", "05/10/2569", "", 25000, 25000, 0, "", "", "REP-0001", "15/11/2569", ""],
                [2, "", "00000002", "03/10/2569", "", "", 1800, 0, 1800, "C01", "ตัวอย่างเหตุผลปฏิเสธ", "", "", ""],
            ];
            for (var r = 0; r < samples.Length; r++)
                for (var c = 0; c < samples[r].Length; c++) help.Cells[sampleRow + 1 + r, c + 1].Value = samples[r][c];
            help.Column(1).Width = 18;

            // sheet ซ่อน: รหัสสิทธิ์ของ template (กันนำเข้าผิดสิทธิ์)
            var meta = package.Workbook.Worksheets.Add(SHEET_META);
            meta.Cells[1, 1].Value = META_SCHEME_KEY;
            meta.Cells[1, 2].Value = schemeCode.ToUpperInvariant();
            if (!string.IsNullOrEmpty(claimBatchNo))
            {
                meta.Cells[2, 1].Value = META_CLAIM_BATCH_KEY;
                meta.Cells[2, 2].Value = claimBatchNo;
                help.Cells[2, 1].Value = $"ชุดเรียกเก็บ {claimBatchNo} — เติม VN/AN และยอดเรียกเก็บไว้แล้ว กรอก \"{COL_RECEIVED}\" (และเหตุผลถ้าไม่จ่าย) แล้วนำเข้า";
            }
            meta.Hidden = eWorkSheetHidden.VeryHidden;

            package.Workbook.Worksheets.MoveToStart(SHEET_DATA);
            return package.GetAsByteArray();
        }

        public static RemittanceParseResult Parse(Stream stream, int maxRows)
        {
            using var package = new ExcelPackage(stream);
            var book = package.Workbook;
            string? scheme = null, claimBatch = null;
            var meta = book.Worksheets[SHEET_META];
            if (meta is not null && string.Equals(meta.Cells[1, 1].Text?.Trim(), META_SCHEME_KEY, StringComparison.OrdinalIgnoreCase))
            {
                scheme = meta.Cells[1, 2].Text?.Trim().ToUpperInvariant();
            }
            if (meta is not null && string.Equals(meta.Cells[2, 1].Text?.Trim(), META_CLAIM_BATCH_KEY, StringComparison.OrdinalIgnoreCase))
            {
                claimBatch = meta.Cells[2, 2].Text?.Trim();
            }

            var ws = book.Worksheets[SHEET_DATA] ?? book.Worksheets.FirstOrDefault(w => w.Name != SHEET_META && w.Hidden == eWorkSheetHidden.Visible);
            if (ws?.Dimension is null) return new(scheme, [], ["ไม่พบข้อมูลในไฟล์"]);

            // หาคอลัมน์จากชื่อหัว (สลับลำดับได้)
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var c = 1; c <= ws.Dimension.End.Column; c++)
            {
                var name = ws.Cells[1, c].Text?.Trim();
                if (!string.IsNullOrEmpty(name) && COLUMNS.Contains(name, StringComparer.OrdinalIgnoreCase)) map.TryAdd(name, c);
            }
            var errors = new List<string>();
            if (!map.ContainsKey(COL_RECEIVED)) errors.Add($"ไม่พบคอลัมน์ \"{COL_RECEIVED}\"");
            if (!map.ContainsKey(COL_VN) && !(map.ContainsKey(COL_HN) && map.ContainsKey(COL_VISIT_DATE)))
                errors.Add($"ต้องมีคอลัมน์ \"{COL_VN}\" หรือ \"{COL_HN}\" + \"{COL_VISIT_DATE}\"");
            if (errors.Count > 0) return new(scheme, [], [.. errors, $"ใช้ template ของระบบ (sheet \"{SHEET_DATA}\" แถวแรกเป็นหัวคอลัมน์)"]);

            var rows = new List<RemittanceRow>();
            var lastRow = ws.Dimension.End.Row;
            for (var r = 2; r <= lastRow; r++)
            {
                object? Value(string col) => map.TryGetValue(col, out var c) ? ws.Cells[r, c].Value : null;
                if (COLUMNS.Where(col => col != COL_NO).All(col => IsBlank(Value(col)))) continue; // แถวว่าง

                if (rows.Count >= maxRows)
                {
                    errors.Add($"ไฟล์มีเกิน {maxRows:N0} แถว — แบ่งไฟล์แล้วนำเข้าทีละไฟล์");
                    break;
                }

                var problems = new List<string>();
                var vn = Text(Value(COL_VN));
                var hn = Text(Value(COL_HN));
                var visitDate = Date(Value(COL_VISIT_DATE), COL_VISIT_DATE, problems);
                var received = Amount(Value(COL_RECEIVED), COL_RECEIVED, problems);
                var denied = Amount(Value(COL_DENIED), COL_DENIED, problems) ?? 0m;
                if (string.IsNullOrEmpty(vn) && (string.IsNullOrEmpty(hn) || visitDate is null))
                    problems.Add($"ต้องมี {COL_VN} หรือ {COL_HN} + {COL_VISIT_DATE}");
                if (received is null && !problems.Any(p => p.Contains(COL_RECEIVED))) problems.Add($"ไม่มี{COL_RECEIVED}");
                if (received < 0 || denied < 0) problems.Add("ยอดติดลบ");

                rows.Add(new RemittanceRow(
                    r, Limit(vn, 20), Limit(hn, 20), visitDate, Date(Value(COL_DISCHARGE_DATE), COL_DISCHARGE_DATE, problems),
                    Limit(Text(Value(COL_BILL)), 30), Amount(Value(COL_CLAIMED), COL_CLAIMED, problems), received ?? 0m, denied,
                    Limit(Text(Value(COL_REASON_CODE)), 50), Limit(Text(Value(COL_REASON_TEXT)), 500), Limit(Text(Value(COL_STATEMENT)), 100),
                    Date(Value(COL_TRANSFER_DATE), COL_TRANSFER_DATE, problems), Limit(Text(Value(COL_NOTE)), 500),
                    problems.Count == 0 ? null : string.Join(" · ", problems.Distinct())));
            }

            if (rows.Count == 0 && errors.Count == 0) errors.Add("ไม่พบแถวข้อมูล");
            return new(scheme, rows, errors, claimBatch);
        }

        private static int ColumnIndex(string name) => COLUMNS.ToList().IndexOf(name) + 1;

        private static bool IsBlank(object? v) => v is null || v is string s && string.IsNullOrWhiteSpace(s);

        /// <summary>ข้อความ — ตัวเลขที่ Excel แปลงจาก HN/VN (เช่น 1234567.0) แปลงกลับเป็นข้อความไม่มีทศนิยม</summary>
        internal static string? Text(object? v) => v switch
        {
            null => null,
            double d when d == Math.Floor(d) && Math.Abs(d) < 1e15 => d.ToString("0", CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            _ => string.IsNullOrWhiteSpace(v.ToString()) ? null : v.ToString()!.Trim(),
        };

        /// <summary>วันที่: DateTime ของ Excel, เลข serial, หรือข้อความ วัน/เดือน/ปี — ปี > 2400 = พ.ศ. (ลบ 543)</summary>
        internal static DateTime? Date(object? v, string column, List<string> problems)
        {
            if (IsBlank(v)) return null;
            DateTime? value = v switch
            {
                DateTime dt => dt,
                double d when d > 0 && d < 2_958_466 => DateTime.FromOADate(d),
                _ => DateTime.TryParseExact(v!.ToString()!.Trim(), DATE_FORMATS, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null,
            };
            if (value is null)
            {
                problems.Add($"{column} ไม่ใช่วันที่ (ใช้ วัน/เดือน/ปี)");
                return null;
            }
            var date = value.Value.Date;
            return date.Year > BUDDHIST_YEAR_THRESHOLD ? date.AddYears(-543) : date;
        }

        internal static decimal? Amount(object? v, string column, List<string> problems)
        {
            if (IsBlank(v)) return null;
            switch (v)
            {
                case double d: return Math.Round((decimal)d, 2);
                case decimal m: return Math.Round(m, 2);
                case int i: return i;
            }
            var text = v!.ToString()!.Replace(",", "").Replace("฿", "").Trim();
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) return Math.Round(parsed, 2);
            problems.Add($"{column} ไม่ใช่ตัวเลข");
            return null;
        }

        private static string? Limit(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
    }
}
