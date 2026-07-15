using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Keyless]
[Table("InterfaceConfig")]
public partial class InterfaceConfig
{
    [Column("UID")]
    public long Uid { get; set; }

    [Column("InterfaceUID")]
    public int InterfaceUid { get; set; }

    [StringLength(255)]
    public string SourceDirectory { get; set; } = null!;

    [StringLength(50)]
    public string FileName { get; set; } = null!;

    [StringLength(50)]
    public string? Subfix { get; set; }

    [StringLength(50)]
    public string CompleteFlagExtention { get; set; } = null!;

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
