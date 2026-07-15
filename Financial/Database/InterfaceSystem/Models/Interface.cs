using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Keyless]
[Table("Interface")]
public partial class Interface
{
    [Column("UID")]
    public long Uid { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = null!;

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [StringLength(255)]
    public string? Description { get; set; }

    [StringLength(8)]
    public string InterfaceType { get; set; } = null!;

    [StringLength(8)]
    public string IsOptional { get; set; } = null!;

    [Column("ParentInterfaceUID")]
    public int? ParentInterfaceUid { get; set; }

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
