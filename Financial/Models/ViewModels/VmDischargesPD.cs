using System.ComponentModel;

namespace Financial.Models.ViewModels
{
    public class VmDischargesPD
    {
        [DisplayName("ลำดับที่")]
        public int Id { get; set; }
        [DisplayName("รายชื่อ")]
        public string FullName { get; set; } = null!;
        [DisplayName("สิทธิ์การรักษา")]
        public string TreatmentRight { get; set; } = null!;
        [DisplayName("เงื่อนไขการเรียกเก็บเงิน")]
        public string BillCon { get; set; } = null!; //billing conditions เงื่อนไขการเรียกเก็บเงิน
        public string HN { get; set; } = null!;
        public string AN { get; set; } = null!;
        public string Admit { get; set; } = null!;
        [DisplayName("D/C")]
        public string DCDate { get; set; } = null!;
        [DisplayName("จำนวนเงิน")]
        public string Amount { get; set; } = null!;
        [DisplayName("คลินิก")]
        public string Clinic { get; set; } = null!; //คลินิก
    }
}
