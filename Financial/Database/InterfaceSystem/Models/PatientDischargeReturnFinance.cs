using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("PatientDischargeReturnFinance")]
public partial class PatientDischargeReturnFinance
{
    [Key]
    [Column("UID")]
    public long Uid { get; set; }

    [Column("PatientDischargeSentAuditorUID")]
    public long PatientDischargeSentAuditorUid { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime RecordDttm { get; set; }

    [Column("STATUSFLAG")]
    [StringLength(1)]
    [Unicode(false)]
    public string Statusflag { get; set; } = null!;
}
