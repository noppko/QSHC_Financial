using System;
using System.Collections.Generic;
using Financial.Database.InterfaceSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace Financial.Database.InterfaceSystem.Contexts;

public partial class InterfaceSystemDbContext : DbContext
{
    public InterfaceSystemDbContext()
    {
    }

    public InterfaceSystemDbContext(DbContextOptions<InterfaceSystemDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CsmbsIpdx> CsmbsIpdxes { get; set; }

    public virtual DbSet<CsmbsIpop> CsmbsIpops { get; set; }

    public virtual DbSet<EquipdevC> EquipdevCs { get; set; }

    public virtual DbSet<Interface> Interfaces { get; set; }

    public virtual DbSet<InterfaceCareproviderLocation> InterfaceCareproviderLocations { get; set; }

    public virtual DbSet<InterfaceConfig> InterfaceConfigs { get; set; }

    public virtual DbSet<InterfaceCsmbslog> InterfaceCsmbslogs { get; set; }

    public virtual DbSet<InterfaceItem> InterfaceItems { get; set; }

    public virtual DbSet<InterfaceItem2> InterfaceItem2s { get; set; }

    public virtual DbSet<InterfaceLog> InterfaceLogs { get; set; }

    public virtual DbSet<InterfaceLogin> InterfaceLogins { get; set; }

    public virtual DbSet<PatientCsmbsaregister> PatientCsmbsaregisters { get; set; }

    public virtual DbSet<PatientDischargeReturnFinance> PatientDischargeReturnFinances { get; set; }

    public virtual DbSet<PatientDischargeReturnFinanceEdit> PatientDischargeReturnFinanceEdits { get; set; }

    public virtual DbSet<PatientDischargeSentAuditor> PatientDischargeSentAuditors { get; set; }

    public virtual DbSet<PatientDischargeSentAuditorEdit> PatientDischargeSentAuditorEdits { get; set; }

    public virtual DbSet<PatientEdcregister> PatientEdcregisters { get; set; }

    public virtual DbSet<QshcLabView> QshcLabViews { get; set; }

    public virtual DbSet<TCsmbsSessionid> TCsmbsSessionids { get; set; }

    public virtual DbSet<TOptellSessionid> TOptellSessionids { get; set; }

    public virtual DbSet<UserLogin> UserLogins { get; set; }

}
