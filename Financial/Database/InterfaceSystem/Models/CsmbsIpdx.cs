using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("CSMBS_IPDX")]
public partial class CsmbsIpdx
{
    [Key]
    [Column("UID")]
    public long Uid { get; set; }

    [Column("AN")]
    [StringLength(10)]
    [Unicode(false)]
    public string An { get; set; } = null!;

    [Column("DXTYPE")]
    [StringLength(50)]
    public string Dxtype { get; set; } = null!;

    [Column("CODESYS")]
    [StringLength(5)]
    public string Codesys { get; set; } = null!;

    [Column("CODE")]
    [StringLength(5)]
    public string Code { get; set; } = null!;

    [Column("DIAGTEAM")]
    public string Diagteam { get; set; } = null!;

    [Column("DR")]
    [StringLength(6)]
    [Unicode(false)]
    public string Dr { get; set; } = null!;

    [Column("DATEDIAG", TypeName = "date")]
    public DateTime Datediag { get; set; }

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
