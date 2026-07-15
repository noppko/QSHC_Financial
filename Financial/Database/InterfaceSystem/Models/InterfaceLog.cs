using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("InterfaceLog")]
public partial class InterfaceLog
{
    [Key]
    [Column("UID")]
    public int Uid { get; set; }

    [Column("InterfaceUID")]
    public int? InterfaceUid { get; set; }

    [Column("InterfaceLoginUID")]
    public int? InterfaceLoginUid { get; set; }

    [StringLength(4)]
    public string? SessionNo { get; set; }

    [Column("DateFromDTTM", TypeName = "datetime")]
    public DateTime DateFromDttm { get; set; }

    [Column("DateToDTTM", TypeName = "datetime")]
    public DateTime DateToDttm { get; set; }

    [Column("CUser")]
    public int? Cuser { get; set; }

    [Column("MUser")]
    public int? Muser { get; set; }

    [Column("CWhen", TypeName = "datetime")]
    public DateTime Cwhen { get; set; }

    [Column("MWhen", TypeName = "datetime")]
    public DateTime Mwhen { get; set; }

    [StringLength(10)]
    public string ProcessStatus { get; set; } = null!;

    [StringLength(1)]
    public string StatusFlag { get; set; } = null!;

    [StringLength(255)]
    public string? Remark { get; set; }
}
