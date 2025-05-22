using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using AttendEase.Models;
using AttendEase.Models.db;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Identity tables are inherited from IdentityDbContext

    // Custom ERD tables
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<LeaveType> LeaveTypes { get; set; }
    public DbSet<LeaveRequest> LeaveRequests { get; set; }
    public DbSet<BranchHoliday> BranchHolidays { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Employee - User (one-to-one)
        builder.Entity<Employee>()
            .HasIndex(e => e.UserId)
            .IsUnique();

        builder.Entity<Employee>()
            .HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Employee>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Admin - Branch (one-to-many)
        builder.Entity<Branch>()
            .HasOne<Admin>()
            .WithMany()
            .HasForeignKey(b => b.AdminId)
            .OnDelete(DeleteBehavior.Restrict);

        // Branch - Employee (one-to-many)
        builder.Entity<Employee>()
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // AttendanceRecord - Employee/Branch (many-to-one)
        builder.Entity<AttendanceRecord>()
            .HasOne<Employee>()
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AttendanceRecord>()
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // LeaveRequest - Employee/LeaveType
        builder.Entity<LeaveRequest>()
            .HasOne<Employee>()
            .WithMany()
            .HasForeignKey(lr => lr.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LeaveRequest>()
            .HasOne<LeaveType>()
            .WithMany()
            .HasForeignKey(lr => lr.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // BranchHoliday - Branch
        builder.Entity<BranchHoliday>()
            .HasOne<Branch>()
            .WithMany()
            .HasForeignKey(h => h.BranchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
