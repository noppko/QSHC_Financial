using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Financial.Database.QSHC.Models
{
    [Table("Employees", Schema = "HIS")]
    public class Employee
    {
        [Key]
        public int EmployeeID { get; set; }

        public string EmployeeCode { get; set; } = null!;

        public int TitleId { get; set; }

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public int? DivisionID { get; set; }

        public int JobPositionID { get; set; }
        //[ForeignKey(nameof(JobPositionID))]


        public virtual JobPosition JobPosition { get; set; } = null!;
        public virtual Division Division { get; set; } = null!;



    }
}
