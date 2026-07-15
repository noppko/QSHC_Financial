using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("InterfaceItem")]
public partial class InterfaceItem
{
    [Key]
    [Column("UID")]
    public int Uid { get; set; }

    [StringLength(100)]
    public string FileName { get; set; } = null!;

    public int Record { get; set; }

    [StringLength(1000)]
    public string Data { get; set; } = null!;

    [StringLength(50)]
    public string InterfaceType { get; set; } = null!;

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

    [StringLength(50)]
    public string? ProcessStatus { get; set; }

    [StringLength(1000)]
    public string? Remark { get; set; }
}
