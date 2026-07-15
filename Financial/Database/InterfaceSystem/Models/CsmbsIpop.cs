using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("CSMBS_IPOP")]
public partial class CsmbsIpop
{
    [Key]
    [Column("UID")]
    public long Uid { get; set; }

    [Column("AN")]
    [StringLength(10)]
    [Unicode(false)]
    public string An { get; set; } = null!;

    [Column("CODESYS")]
    [StringLength(6)]
    public string Codesys { get; set; } = null!;

    [Column("CODE")]
    [StringLength(10)]
    public string Code { get; set; } = null!;

    [Column("PROCTERM")]
    public string Procterm { get; set; } = null!;

    [Column("DR")]
    [StringLength(6)]
    [Unicode(false)]
    public string? Dr { get; set; }

    [Column("DATEIN", TypeName = "datetime")]
    public DateTime? Datein { get; set; }

    [Column("DATEOUT", TypeName = "datetime")]
    public DateTime? Dateout { get; set; }

    [Column("LOCATION")]
    [StringLength(15)]
    public string Location { get; set; } = null!;

    [Column("CUser")]
    public int Cuser { get; set; }

    [Column("CWhen", TypeName = "datetime")]
    public DateTime Cwhen { get; set; }

    [Column("MUser")]
    public int Muser { get; set; }

    [Column("MWhen", TypeName = "datetime")]
    public DateTime Mwhen { get; set; }

    [StringLength(1)]
    public string StatusFlag { get; set; } = null!;

    [Column("TIMESTAMP")]
    public byte[] Timestamp { get; set; } = null!;
}
