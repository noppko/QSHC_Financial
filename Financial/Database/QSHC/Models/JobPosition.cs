using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Financial.Database.QSHC.Models
{
    [Table("JobPositions", Schema = "HIS")]
    public class JobPosition
    {
        
        //public JobPosition()
        //{
        //    Employees = new HashSet<Employee>();
        //}

        [Key]
        public int JobPositionID { get; set; }
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = null!;
        [Required]
        [StringLength(2)]
        public string Abbrevation { get; set; } = null!;
        [StringLength(50)]
        public string? Description { get; set; }


        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
