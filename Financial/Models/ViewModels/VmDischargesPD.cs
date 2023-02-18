using System.ComponentModel;

namespace Financial.Models.ViewModels
{
    public class VmDischargesPD
    {
        [DisplayName("ลำดับที่")]
        public int Id { get; set; }
        [DisplayName("รายชื่อ")]
        public string FullName { get; set; }
        [DisplayName("สิทธิ์การรักษา")]
        public string TreatmentRight { get; set; }
        [DisplayName("เงื่อนไขการเรียกเก็บเงิน")]
        public string BillCon { get; set; } //billing conditions เงื่อนไขการเรียกเก็บเงิน
        public string HN { get; set; }
        public string AN { get; set; }
        public string Admit { get; set; }
        [DisplayName("D/C")]
        public string DCDate { get; set; }
        [DisplayName("จำนวนเงิน")]
        public string Amount { get; set; }
        [DisplayName("คลินิก")]
        public string Clinic { get; set; } //คลินิก
    }
}
