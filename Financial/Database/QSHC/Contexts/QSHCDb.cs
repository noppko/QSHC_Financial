using Microsoft.EntityFrameworkCore;
using Financial.Database.QSHC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Financial.Database.QSHC.Contexts
{
    public class QSHCDb : DbContext
    {
        public QSHCDb(DbContextOptions<QSHCDb> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Title> Titles { get; set; }
        public DbSet<Sex> Sexes { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Division> Divisions { get; set; }
        public DbSet<JobPosition> JobPositions { get; set; }
        public DbSet<UserStatus> UserStatuses { get; set; }

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<Employee>(entity =>
        //    {
        //        entity.ToTable("Employees", "HIS", tb => tb.HasTrigger("AccSync"));
        //    });
        //}

    }
}
