using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Financial.Database.QSHC.Models
{
    [Table("Divisions", Schema = "HIS")]
    public class Division
    {
        //public Division()
        //{
        //    Employees = new HashSet<Employee>();
        //}

        [Key]
        public int DivisionID { get; set; }
        [Required]
        [StringLength(50)]
        public string ThaiName { get; set; } = null!;
        [StringLength(50)]
        public string EnglishName { get; set; } = null!;
        [Required]
        [StringLength(6)]
        public string Abbrevation { get; set; } = null!;
        [StringLength(50)]
        public string Description { get; set; } = null!;
        [Column("DepartmentID")]
        [ForeignKey(nameof(DepartmentId))]
        public int? DepartmentId { get; set; }

        
        //[InverseProperty(nameof(Departments.Divisions))]
        //public virtual Department Department { get; set; }
        //[InverseProperty("Division")]
        //public virtual ICollection<Employee> Employees { get; set; }
    }
}
