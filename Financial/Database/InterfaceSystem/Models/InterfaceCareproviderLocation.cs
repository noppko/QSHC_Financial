using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("Interface_CareproviderLocation")]
public partial class InterfaceCareproviderLocation
{
    [Key]
    [Column("UID")]
    public int Uid { get; set; }

    [Column("CareProviderUID")]
    public int CareProviderUid { get; set; }

    [StringLength(2)]
    public string CareProviderLocationCode { get; set; } = null!;

    [StringLength(100)]
    public string CareProviderLocationName { get; set; } = null!;

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
