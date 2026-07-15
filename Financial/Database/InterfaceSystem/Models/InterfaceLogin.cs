using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Keyless]
[Table("InterfaceLogin")]
public partial class InterfaceLogin
{
    [Column("UID")]
    public long Uid { get; set; }

    [StringLength(50)]
    public string LoginName { get; set; } = null!;

    [StringLength(50)]
    public string Password { get; set; } = null!;

    [Column("FName")]
    [StringLength(50)]
    public string? Fname { get; set; }

    [Column("LName")]
    [StringLength(50)]
    public string? Lname { get; set; }

    [StringLength(1)]
    public string IsLocked { get; set; } = null!;

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

    [Column("InterfaceUID")]
    public int? InterfaceUid { get; set; }
}
