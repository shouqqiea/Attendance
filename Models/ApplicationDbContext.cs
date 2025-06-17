using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using Microsoft.AspNetCore.Identity;

namespace LetsCheckIn.Models
{
    // ✅ Updated to not use IdentityRole - relying on Dynamic roles only
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Identity tables are inherited from IdentityDbContext

        // Custom ERD tables
        public DbSet<Admin> Admin { get; set; }
        public DbSet<Branch> Branch { get; set; }
        public DbSet<Employee> Employee { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecord { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<BranchHoliday> BranchHoliday { get; set; }
        public DbSet<BranchSetup> BranchSetup { get; set; }
        public DbSet<BranchAllowedLocation> BranchAllowedLocation { get; set; }
        public DbSet<BranchAllowedNetwork> BranchAllowedNetwork { get; set; }
        public DbSet<BranchAllowedFace> BranchAllowedFace { get; set; }
        public DbSet<StatusType> StatusTypes { get; set; }

        // Dynamic Role & Permission tables
        public DbSet<Role> DynamicRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<DynamicUserRole> DynamicUserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<BranchRole> BranchRoles { get; set; }

        // Security and Audit tables
        public DbSet<SecurityAuditLog> SecurityAuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure table names to match existing database
            builder.Entity<Admin>().ToTable("Admin");
            builder.Entity<Branch>().ToTable("Branch");
            builder.Entity<Employee>().ToTable("Employee");
            builder.Entity<AttendanceRecord>().ToTable("AttendanceRecord");
            builder.Entity<LeaveType>().ToTable("LeaveTypes");
            builder.Entity<LeaveRequest>().ToTable("LeaveRequests");
            builder.Entity<BranchHoliday>().ToTable("BranchHoliday");
            builder.Entity<BranchSetup>().ToTable("BranchSetup");
            builder.Entity<BranchAllowedLocation>().ToTable("BranchAllowedLocation");
            builder.Entity<BranchAllowedNetwork>().ToTable("BranchAllowedNetwork");
            builder.Entity<BranchAllowedFace>().ToTable("BranchAllowedFace");

            // Configure dynamic role and permission tables
            builder.Entity<Role>().ToTable("DynamicRoles");
            builder.Entity<Permission>().ToTable("Permissions");
            builder.Entity<DynamicUserRole>().ToTable("DynamicUserRoles");
            builder.Entity<RolePermission>().ToTable("RolePermissions");
            builder.Entity<BranchRole>().ToTable("BranchRoles");

            // Configure security audit log table
            builder.Entity<SecurityAuditLog>().ToTable("SecurityAuditLogs");

            // Employee - User (one-to-one)
            builder.Entity<Employee>()
                .HasOne(e => e.User)
                .WithOne(u => u.Employee)
                .HasForeignKey<Employee>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Remove any existing index on UserId if it exists
            builder.Entity<Employee>()
                .HasIndex(e => e.UserId)
                .IsUnique();

            // Admin - Branch (one-to-many, explicit navigation)
            builder.Entity<Branch>()
                .HasOne(b => b.Admin)
                .WithMany(a => a.Branches)
                .HasForeignKey(b => b.AdminId)
                .OnDelete(DeleteBehavior.Restrict);

            // Branch Hierarchy - Parent-Child relationships
            builder.Entity<Branch>()
                .HasOne(b => b.ParentBranch)
                .WithMany(b => b.ChildBranches)
                .HasForeignKey(b => b.ParentBranchId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete to preserve hierarchy

            // Add index on ParentBranchId for performance
            builder.Entity<Branch>()
                .HasIndex(b => b.ParentBranchId)
                .HasDatabaseName("IX_Branch_ParentBranchId");

            // Add index on BranchType for filtering queries
            builder.Entity<Branch>()
                .HasIndex(b => b.BranchType)
                .HasDatabaseName("IX_Branch_BranchType");

            // Branch - Employee (one-to-many)
            builder.Entity<Employee>()
                .HasOne(e => e.Branch)
                .WithMany(b => b.Employees)
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // AttendanceRecord - Employee/Branch (many-to-one)
            builder.Entity<AttendanceRecord>()
                .HasOne(a => a.Employee)
                .WithMany(e => e.AttendanceRecords)
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AttendanceRecord>()
                .HasOne(a => a.Branch)
                .WithMany(b => b.AttendanceRecords)
                .HasForeignKey(a => a.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // LeaveRequest - Employee/LeaveType
            builder.Entity<LeaveRequest>()
                .HasOne(lr => lr.Employee)
                .WithMany(e => e.LeaveRequests)
                .HasForeignKey(lr => lr.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<LeaveRequest>()
                .HasOne(lr => lr.LeaveType)
                .WithMany(lt => lt.LeaveRequests)
                .HasForeignKey(lr => lr.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // BranchHoliday - Branch
            builder.Entity<BranchHoliday>()
                .HasOne(h => h.Branch)
                .WithMany(b => b.BranchHolidays)
                .HasForeignKey(h => h.BranchId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent multiple cascade paths

            // Branch - BranchSetup (one-to-one)
            builder.Entity<Branch>()
                .HasOne(b => b.BranchSetup)
                .WithOne(bs => bs.Branch)
                .HasForeignKey<BranchSetup>(bs => bs.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            // BranchSetup - AllowedLocations (one-to-many)
            builder.Entity<BranchAllowedLocation>()
                .HasOne(l => l.BranchSetup)
                .WithMany(bs => bs.AllowedLocations)
                .HasForeignKey(l => l.SetupId)
                .OnDelete(DeleteBehavior.Cascade);

            // BranchSetup - AllowedNetworks (one-to-many)
            builder.Entity<BranchAllowedNetwork>()
                .HasOne(n => n.BranchSetup)
                .WithMany(bs => bs.AllowedNetworks)
                .HasForeignKey(n => n.SetupId)
                .OnDelete(DeleteBehavior.Cascade);

            // BranchSetup - AllowedFaces (one-to-many)
            builder.Entity<BranchAllowedFace>()
                .HasOne(f => f.BranchSetup)
                .WithMany(bs => bs.AllowedFaces)
                .HasForeignKey(f => f.SetupId)
                .OnDelete(DeleteBehavior.Cascade);

            // AllowedLocation - Employee (optional)
            builder.Entity<BranchAllowedLocation>()
                .HasOne(l => l.Employee)
                .WithMany()
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // AllowedNetwork - Employee (optional)
            builder.Entity<BranchAllowedNetwork>()
                .HasOne(n => n.Employee)
                .WithMany()
                .HasForeignKey(n => n.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // AllowedFace - Employee (required)
            builder.Entity<BranchAllowedFace>()
                .HasOne(f => f.Employee)
                .WithMany()
                .HasForeignKey(f => f.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure StatusType and LeaveRequest relationship
            builder.Entity<LeaveRequest>()
                .HasOne(lr => lr.Status)
                .WithMany(st => st.LeaveRequests)
                .HasForeignKey(lr => lr.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            // Dynamic Role & Permission relationships
            
            // UserRole - User
            builder.Entity<DynamicUserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserRole - Role
            builder.Entity<DynamicUserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // RolePermission - Role
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // RolePermission - Permission
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // BranchRole - Branch
            builder.Entity<BranchRole>()
                .HasOne(br => br.Branch)
                .WithMany()
                .HasForeignKey(br => br.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            // BranchRole - Role
            builder.Entity<BranchRole>()
                .HasOne(br => br.Role)
                .WithMany(r => r.BranchRoles)
                .HasForeignKey(br => br.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraints
            builder.Entity<DynamicUserRole>()
                .HasIndex(ur => new { ur.UserId, ur.RoleId })
                .IsUnique();

            builder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.RoleId, rp.PermissionId })
                .IsUnique();

            builder.Entity<BranchRole>()
                .HasIndex(br => new { br.BranchId, br.RoleId })
                .IsUnique();

            // SecurityAuditLog - User relationship
            builder.Entity<SecurityAuditLog>()
                .HasOne(sal => sal.User)
                .WithMany()
                .HasForeignKey(sal => sal.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Security audit log indexes for performance
            builder.Entity<SecurityAuditLog>()
                .HasIndex(sal => sal.UserId)
                .HasDatabaseName("IX_SecurityAuditLog_UserId");

            builder.Entity<SecurityAuditLog>()
                .HasIndex(sal => sal.Timestamp)
                .HasDatabaseName("IX_SecurityAuditLog_Timestamp");

            builder.Entity<SecurityAuditLog>()
                .HasIndex(sal => new { sal.Action, sal.ResourceType })
                .HasDatabaseName("IX_SecurityAuditLog_Action_ResourceType");

            builder.Entity<SecurityAuditLog>()
                .HasIndex(sal => sal.IsSecurityViolation)
                .HasDatabaseName("IX_SecurityAuditLog_IsSecurityViolation");

            // Seed initial status types
            builder.Entity<StatusType>().HasData(
                new StatusType { StatusId = 1, StatusName = "pending" },
                new StatusType { StatusId = 2, StatusName = "approved" },
                new StatusType { StatusId = 3, StatusName = "rejected" },
                new StatusType { StatusId = 4, StatusName = "emergency" },
                new StatusType { StatusId = 5, StatusName = "cancelled" }
            );
        }
    }
}
