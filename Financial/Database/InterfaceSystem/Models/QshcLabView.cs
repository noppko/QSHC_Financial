using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Keyless]
public partial class QshcLabView
{
    [Column("LAB_NAME")]
    [StringLength(255)]
    public string? LabName { get; set; }

    [Column("VN")]
    [StringLength(20)]
    public string? Vn { get; set; }

    [Column("ORDER_STATUS")]
    [StringLength(255)]
    public string? OrderStatus { get; set; }

    [Column("ORDER_PRIORITY")]
    [StringLength(255)]
    public string? OrderPriority { get; set; }

    [Column("VISIT_TYPE")]
    [StringLength(255)]
    public string? VisitType { get; set; }
}
