using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("PatientCSMBSARegister")]
public partial class PatientCsmbsaregister
{
    [Column("UID")]
    public int Uid { get; set; }

    [Key]
    [Column("AUTHCODE")]
    [StringLength(6)]
    public string Authcode { get; set; } = null!;

    [Column("HN")]
    [StringLength(8)]
    public string? Hn { get; set; }

    [Column("AN")]
    [StringLength(9)]
    public string? An { get; set; }

    [Column("NOM")]
    [StringLength(50)]
    public string? Nom { get; set; }

    [Column("BOD")]
    [StringLength(10)]
    public string? Bod { get; set; }

    [Column("ADMITDT")]
    [StringLength(10)]
    public string? Admitdt { get; set; }

    [Column("ADMITTM")]
    [StringLength(5)]
    public string? Admittm { get; set; }

    [Column("ISSUEDT")]
    [StringLength(10)]
    public string? Issuedt { get; set; }

    [Column("ISSUETM")]
    [StringLength(5)]
    public string? Issuetm { get; set; }

    [Column("EXPDT")]
    [StringLength(10)]
    public string? Expdt { get; set; }

    [Column("EXPTM")]
    [StringLength(5)]
    public string? Exptm { get; set; }

    [Column("PID")]
    [StringLength(13)]
    public string? Pid { get; set; }

    [Column("TIMESTAM2")]
    [StringLength(19)]
    public string? Timestam2 { get; set; }

    [Column("CUser")]
    public int Cuser { get; set; }

    [Column("MUser")]
    public int Muser { get; set; }

    [Column("CWhen", TypeName = "datetime")]
    public DateTime Cwhen { get; set; }

    [Column("MWhen", TypeName = "datetime")]
    public DateTime Mwhen { get; set; }

    [StringLength(1)]
    public string StatusFlag { get; set; } = null!;

    [Column("TIMESTAMP")]
    public byte[] Timestamp { get; set; } = null!;
}
