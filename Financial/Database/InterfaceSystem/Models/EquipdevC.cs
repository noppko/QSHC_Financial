using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Keyless]
[Table("Equipdev_CS")]
public partial class EquipdevC
{
    [Column("code")]
    [StringLength(255)]
    public string? Code { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string? Description { get; set; }

    [Column("revclass")]
    [StringLength(255)]
    public string? Revclass { get; set; }

    [Column("revrate")]
    [StringLength(255)]
    public string? Revrate { get; set; }

    [Column("dateeff", TypeName = "datetime")]
    public DateTime? Dateeff { get; set; }

    [Column("dateexp", TypeName = "datetime")]
    public DateTime? Dateexp { get; set; }

    [Column("exdrgpay")]
    [StringLength(255)]
    public string? Exdrgpay { get; set; }

    [Column("remark")]
    [StringLength(255)]
    public string? Remark { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Mwhen { get; set; }

    [StringLength(255)]
    public string? หมวด { get; set; }

    [Column("อัตราเบิกได้")]
    public double? อตราเบกได { get; set; }

    [Column("daterev", TypeName = "datetime")]
    public DateTime? Daterev { get; set; }
}
