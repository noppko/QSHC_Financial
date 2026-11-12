using Financial.Models;
using Financial.Models.ViewModels;
using System.Data;

namespace Financial.Services.Discharges
{
    /// <summary>
    /// แปลงผลลัพธ์จาก Stored Procedure (ชื่อคอลัมน์ภาษาไทย) เป็น VmDischargesPD
    /// หา ordinal ครั้งเดียวต่อ result set แทนการค้นหาด้วยชื่อทุกแถว
    /// อ่านคอลัมน์ "เรียงตาม ordinal จากซ้ายไปขวา" เสมอ — DischargeService เปิด reader แบบ SequentialAccess
    /// ซึ่งอ่านย้อนกลับ/ข้ามไปแล้วกลับมาไม่ได้ (InvalidOperationException) ไม่ว่า SP จะเรียงคอลัมน์อย่างไร
    /// </summary>
    public static class DischargeRecordMapper
    {
        public static List<VmDischargesPD> ReadAll(IDataReader reader, string type)
        {
            var results = new List<VmDischargesPD>();

            int Ord(string name) => reader.GetOrdinal(name);
            // คอลัมน์ที่มีเฉพาะ SP ตัวใหม่ (…IPDAllPayor) — ตัวเดิมไม่มี จึงคืน -1
            int OptionalOrd(string name) => Enumerable.Range(0, reader.FieldCount).FirstOrDefault(i => reader.GetName(i) == name, -1);

            int id = Ord("ลำดับที่"), fullName = Ord("รายชื่อ"), right = Ord("สิทธิ์การรักษา"), hn = Ord("HN"), amount = Ord("จำนวนเงิน");
            var ipd = type == PatientTypeCodes.IPD;

            // IPD: AN, Admit, D/C (+ เงื่อนไขการเรียกเก็บ/หอผู้ป่วยถ้า SP มี) · OPD: VN, วันที่, เงื่อนไขการเรียกเก็บ, คลินิก
            int number = ipd ? Ord("AN") : Ord("VN"),
                admit = ipd ? Ord("Admit") : -1,
                date = ipd ? Ord("D/C") : Ord("วันที่"),
                billCon = ipd ? OptionalOrd("เงื่อนไขการเรียกเก็บ") : Ord("เงื่อนไขการเรียกเก็บ"),
                place = ipd ? OptionalOrd("หอผู้ป่วย") : Ord("คลินิก");

            // ลำดับการอ่าน = ordinal จากน้อยไปมาก
            var order = new[] { id, fullName, right, hn, amount, number, admit, date, billCon, place }
                .Where(o => o >= 0).Distinct().OrderBy(o => o).ToArray();
            var text = new Dictionary<int, string>(order.Length);

            while (reader.Read())
            {
                text.Clear();
                foreach (var o in order) text[o] = Str(reader, o);

                string Get(int ordinal) => ordinal >= 0 ? text[ordinal] : string.Empty;
                results.Add(new VmDischargesPD
                {
                    Id = int.TryParse(Get(id), out var n) ? n : 0,
                    FullName = Get(fullName),
                    TreatmentRight = Get(right),
                    BillCon = Get(billCon),
                    HN = Get(hn),
                    AN = Get(number),
                    Admit = Get(admit),
                    DCDate = Get(date),
                    Amount = Get(amount),
                    Clinic = Get(place),
                });
            }

            return results;
        }

        private static string Str(IDataRecord r, int i) => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }
}
