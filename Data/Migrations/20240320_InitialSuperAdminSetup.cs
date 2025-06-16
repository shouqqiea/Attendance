using Microsoft.EntityFrameworkCore.Migrations;

namespace LetsCheckIn.Data.Migrations
{
    public partial class InitialSuperAdminSetup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Main Branch as SuperAdmin branch
            migrationBuilder.Sql(@"
                INSERT INTO Branch (BranchName, AdminId, CreatedDate, IsSuperAdminBranch)
                VALUES ('Main Branch', 1, GETUTCDATE(), 1)
            ");

            // Create BranchSetup for Main Branch
            migrationBuilder.Sql(@"
                INSERT INTO BranchSetup (BranchId, EnableLocation, EnableNetwork, EnableBiometric, CreatedDate)
                VALUES (
                    (SELECT BranchId FROM Branch WHERE IsSuperAdminBranch = 1),
                    1, 1, 1, GETUTCDATE()
                )
            ");

            // Create SuperAdmin role
            migrationBuilder.Sql(@"
                INSERT INTO DynamicRoles (RoleName, Description, IsSystemRole, IsActive, CreatedDate)
                VALUES ('SuperAdmin', 'Super Administrator with full system access', 1, 1, GETUTCDATE())
            ");

            // Create Admin role
            migrationBuilder.Sql(@"
                INSERT INTO DynamicRoles (RoleName, Description, IsSystemRole, IsActive, CreatedDate)
                VALUES ('Admin', 'Branch Administrator with branch-level access', 1, 1, GETUTCDATE())
            ");

            // Assign SuperAdmin role to Main Branch
            migrationBuilder.Sql(@"
                INSERT INTO BranchRoles (BranchId, RoleId, AssignedBy, AssignedDate, IsActive)
                VALUES (
                    (SELECT BranchId FROM Branch WHERE IsSuperAdminBranch = 1),
                    (SELECT RoleId FROM DynamicRoles WHERE RoleName = 'SuperAdmin'),
                    'System',
                    GETUTCDATE(),
                    1
                )
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the SuperAdmin role assignment
            migrationBuilder.Sql(@"
                DELETE FROM BranchRoles 
                WHERE RoleId = (SELECT RoleId FROM DynamicRoles WHERE RoleName = 'SuperAdmin')
            ");

            // Remove the roles
            migrationBuilder.Sql(@"
                DELETE FROM DynamicRoles WHERE RoleName IN ('SuperAdmin', 'Admin')
            ");

            // Remove the branch setup
            migrationBuilder.Sql(@"
                DELETE FROM BranchSetup 
                WHERE BranchId = (SELECT BranchId FROM Branch WHERE IsSuperAdminBranch = 1)
            ");

            // Remove the Main Branch
            migrationBuilder.Sql(@"
                DELETE FROM Branch WHERE IsSuperAdminBranch = 1
            ");
        }
    }
} 