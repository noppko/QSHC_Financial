using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Financial.Database.QSHC.Models
{
    //[Table("Sexes", Schema = "HIS")]
    public class Sex
    {
        //public Sex()
        //{
        //    Employees = new HashSet<Employee>();
        //}

        [Key]
        [Column("SexID")]
        public int SexId { get; set; }
        [Required]
        [StringLength(10)]
        public string Name { get; set; }

        //[InverseProperty("Sex")]
        //public virtual ICollection<Employee> Employees { get; set; }
    }
}
