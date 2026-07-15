using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Keyless]
[Table("UserLogin")]
public partial class UserLogin
{
    [Column("ID")]
    public long Id { get; set; }

    [Column("FName")]
    [StringLength(50)]
    public string? Fname { get; set; }

    [Column("LName")]
    [StringLength(50)]
    public string? Lname { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string Email { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string Password { get; set; } = null!;

    [StringLength(1)]
    public string StatusFlag { get; set; } = null!;
}
