using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Financial.Database.QSHC.Models
{
    //[Table("Departments", Schema = "HIS")]
    public class Department
    {
        //public Department()
        //{
        //    Divisions = new HashSet<Division>();
        //}

        [Key]
        [Column("DepartmentID")]
        public int DepartmentId { get; set; }
        [Required]
        [StringLength(50)]
        public string ThaiName { get; set; }
        [StringLength(50)]
        public string EnglishName { get; set; }
        [StringLength(10)]
        public string Abbrevation { get; set; }
        [StringLength(50)]
        public string Description { get; set; }

        //[InverseProperty("Department")]
        //public virtual ICollection<Division> Divisions { get; set; }
    }
}
