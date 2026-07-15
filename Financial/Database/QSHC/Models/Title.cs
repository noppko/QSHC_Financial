using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Financial.Database.QSHC.Models
{
    //[Table("Titles", Schema = "HIS")]
    public class Title
    {
        //public Title()
        //{
        //    Employees = new HashSet<Employee>();
        //}

        [Key]
        [Column("TitleID")]
        public int TitleId { get; set; }
        [Required]
        [StringLength(3)]
        public string TitleCode { get; set; }
        [StringLength(50)]
        public string Abbreviation { get; set; }
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        [StringLength(50)]
        public string Description { get; set; }

        //[InverseProperty("Title")]
        //public virtual ICollection<Employee> Employees { get; set; }
    }
}
