using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("PatientDischargeSentAuditor")]
public partial class PatientDischargeSentAuditor
{
    [Key]
    [Column("UID")]
    public long Uid { get; set; }

    [Column("PATIENT_NAME")]
    [StringLength(250)]
    [Unicode(false)]
    public string? PatientName { get; set; }

    [Column("PAYOR_NAME")]
    [StringLength(250)]
    [Unicode(false)]
    public string? PayorName { get; set; }

    [Column("HN")]
    [StringLength(8)]
    [Unicode(false)]
    public string? Hn { get; set; }

    [Column("AN")]
    [StringLength(10)]
    [Unicode(false)]
    public string? An { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? AdmissionDttm { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? MedicalDischargeDttm { get; set; }

    [Column("AMOUNT")]
    [StringLength(20)]
    [Unicode(false)]
    public string? Amount { get; set; }

    [Column("COMMENT")]
    [StringLength(250)]
    [Unicode(false)]
    public string? Comment { get; set; }

    [Column("STATUSFLAG")]
    [StringLength(1)]
    [Unicode(false)]
    public string? Statusflag { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RecordDttm { get; set; }
}
