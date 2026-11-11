using Financial.Models;
using Financial.Models.ViewModels;
using System.Data;

namespace Financial.Services.Discharges
{
    /// <summary>
    /// แปลงผลลัพธ์จาก Stored Procedure (ชื่อคอลัมน์ภาษาไทย) เป็น VmDischargesPD
    /// หา ordinal ครั้งเดียวต่อ result set แทนการค้นหาด้วยชื่อทุกแถว
    /// </summary>
    public static class DischargeRecordMapper
    {
        public static List<VmDischargesPD> ReadAll(IDataReader reader, string type)
        {
            var results = new List<VmDischargesPD>();

            int Ord(string name) => reader.GetOrdinal(name);

            int id = Ord("ลำดับที่"), fullName = Ord("รายชื่อ"), right = Ord("สิทธิ์การรักษา"), hn = Ord("HN"), amount = Ord("จำนวนเงิน");

            if (type == PatientTypeCodes.IPD)
            {
                int an = Ord("AN"), admit = Ord("Admit"), dc = Ord("D/C");
                while (reader.Read())
                {
                    results.Add(new VmDischargesPD
                    {
                        Id = Int(reader, id),
                        FullName = Str(reader, fullName),
                        TreatmentRight = Str(reader, right),
                        HN = Str(reader, hn),
                        AN = Str(reader, an),
                        Admit = Str(reader, admit),
                        DCDate = Str(reader, dc),
                        Amount = Str(reader, amount),
                        BillCon = string.Empty,
                        Clinic = string.Empty,
                    });
                }
            }
            else
            {
                int billCon = Ord("เงื่อนไขการเรียกเก็บ"), vn = Ord("VN"), date = Ord("วันที่"), clinic = Ord("คลินิก");
                while (reader.Read())
                {
                    results.Add(new VmDischargesPD
                    {
                        Id = Int(reader, id),
                        FullName = Str(reader, fullName),
                        TreatmentRight = Str(reader, right),
                        BillCon = Str(reader, billCon),
                        HN = Str(reader, hn),
                        AN = Str(reader, vn),
                        Admit = string.Empty,
                        DCDate = Str(reader, date),
                        Amount = Str(reader, amount),
                        Clinic = Str(reader, clinic),
                    });
                }
            }

            return results;
        }

        private static string Str(IDataRecord r, int i) => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i))?.Trim() ?? string.Empty;

        private static int Int(IDataRecord r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
    }
}
