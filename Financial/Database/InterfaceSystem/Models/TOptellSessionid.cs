using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Models;

[Keyless]
[Table("T_OPTELL_SESSIONID")]
public partial class TOptellSessionid
{
    [Column("UID")]
    public int Uid { get; set; }

    [StringLength(1)]
    [Unicode(false)]
    public string? StatusFlag { get; set; }
}
