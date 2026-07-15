using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Table("PatientEDCRegister")]
public partial class PatientEdcregister
{
    [Key]
    [Column("UID")]
    public int Uid { get; set; }

    [StringLength(5)]
    public string? Code { get; set; }

    [StringLength(8)]
    public string? CompanyCode { get; set; }

    [StringLength(50)]
    public string? CompanyName { get; set; }

    [StringLength(10)]
    public string? MerchantNo { get; set; }

    [StringLength(50)]
    public string? MerchantName { get; set; }

    [StringLength(1)]
    public string? Htype { get; set; }

    [Column("TerminalID")]
    [StringLength(10)]
    public string? TerminalId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime TransactionDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? PostDate { get; set; }

    [Column("PersonalID")]
    [StringLength(13)]
    public string PersonalId { get; set; } = null!;

    [StringLength(50)]
    public string? Name { get; set; }

    [StringLength(50)]
    public string? Surname { get; set; }

    [Column("RefPID")]
    [StringLength(13)]
    public string? RefPid { get; set; }

    [Column("PIDCus")]
    [StringLength(13)]
    public string? Pidcus { get; set; }

    [StringLength(50)]
    public string? CusName { get; set; }

    [StringLength(50)]
    public string? CusSurname { get; set; }

    [Column("PAIDType1")]
    [StringLength(10)]
    public string? Paidtype1 { get; set; }

    [Column("PAIDType2")]
    [StringLength(10)]
    public string? Paidtype2 { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? BirthDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? EditDate { get; set; }

    [Column("TranAMT", TypeName = "decimal(18, 2)")]
    public decimal TranAmt { get; set; }

    [StringLength(10)]
    public string? Batch { get; set; }

    [StringLength(20)]
    public string? Trace { get; set; }

    [StringLength(20)]
    public string? TxnsCode { get; set; }

    [StringLength(20)]
    public string AppCode { get; set; } = null!;

    [Column("TransRefID")]
    [StringLength(20)]
    public string? TransRefId { get; set; }

    [Column("UserID")]
    [StringLength(50)]
    public string? UserId { get; set; }

    [Column("CUser")]
    public string? Cuser { get; set; }

    [Column("MUser")]
    public string? Muser { get; set; }

    [Column("CWhen", TypeName = "datetime")]
    public DateTime Cwhen { get; set; }

    [Column("MWhen", TypeName = "datetime")]
    public DateTime Mwhen { get; set; }

    [StringLength(1)]
    public string StatusFlag { get; set; } = null!;

    [Column("TIMESTAMP")]
    public byte[] Timestamp { get; set; } = null!;
}
