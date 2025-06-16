using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LetsCheckIn.Helpers;

public static class DataSeeder
{
    public static async Task SeedRolesAndSuperAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var permissionService = serviceProvider.GetRequiredService<LetsCheckIn.Helpers.IDynamicPermissionService>();

        string[] roles = { "SuperAdmin", "Admin", "Manager", "Employee" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole { Name = role });
        }

        // Initialize dynamic permissions and roles
        await permissionService.SeedDefaultPermissionsAsync();
        await permissionService.MigrateExistingRolesAsync();
        await permissionService.MigrateExistingUsersAsync();

        // Create/Update default Main Branch as superadmin branch if none exists
        var mainBranch = await dbContext.Branch.FirstOrDefaultAsync(b => b.BranchName == "Main Branch");
        
        if (mainBranch == null)
        {
            // Create default admin
            var defaultAdmin = new Admin 
            { 
                Name = "SuperAdmin User",
                Email = "superadmin@company.com"
            };
            dbContext.Admin.Add(defaultAdmin);
            await dbContext.SaveChangesAsync();

            // Create Main Branch as superadmin branch
            mainBranch = new Branch
            {
                BranchName = "Main Branch",
                AdminId = defaultAdmin.AdminId,
                CreatedDate = DateTime.UtcNow,
                IsSuperAdminBranch = true // Mark as superadmin branch
            };
            dbContext.Branch.Add(mainBranch);
            await dbContext.SaveChangesAsync();

            // Create default branch setup
            var defaultSetup = new BranchSetup
            {
                BranchId = mainBranch.BranchId,
                EnableLocation = true,
                EnableNetwork = false,
                EnableBiometric = false,
                CreatedDate = DateTime.UtcNow
            };
            dbContext.BranchSetup.Add(defaultSetup);
            await dbContext.SaveChangesAsync();
        }
        else if (!mainBranch.IsSuperAdminBranch)
        {
            // Update existing Main Branch to be superadmin branch
            mainBranch.IsSuperAdminBranch = true;
            await dbContext.SaveChangesAsync();
        }

        var superAdminEmail = "superadmin@company.com";
        var superAdminUser = await userManager.FindByEmailAsync(superAdminEmail);

        if (superAdminUser == null)
        {
            superAdminUser = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                EmailConfirmed = true,
                BranchId = mainBranch.BranchId
            };

            var result = await userManager.CreateAsync(superAdminUser, "SuperAdmin123!");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(superAdminUser, "SuperAdmin");
                
                // Create Employee record for SuperAdmin
                var superAdminEmployee = new Employee
                {
                    UserId = superAdminUser.Id,
                    FirstName = "Super",
                    LastName = "Admin",
                    Email = superAdminEmail,
                    BranchId = mainBranch.BranchId,
                    Status = true,
                    CreatedDate = DateTime.UtcNow
                };
                dbContext.Employee.Add(superAdminEmployee);
                await dbContext.SaveChangesAsync();

                // Assign SuperAdmin role in dynamic system
                var superAdminRole = await dbContext.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == "SuperAdmin");
                if (superAdminRole != null)
                {
                    await permissionService.AssignRoleToUserAsync(superAdminUser.Id, superAdminRole.RoleId, "System");
                }
            }
        }
        else
        {
            // Ensure existing SuperAdmin is on the Main Branch
            var superAdminEmployee = await dbContext.Employee.FirstOrDefaultAsync(e => e.UserId == superAdminUser.Id);
            if (superAdminEmployee == null)
            {
                // Create Employee record if it doesn't exist
                superAdminEmployee = new Employee
                {
                    UserId = superAdminUser.Id,
                    FirstName = "Super",
                    LastName = "Admin",
                    Email = superAdminEmail,
                    BranchId = mainBranch.BranchId,
                    Status = true,
                    CreatedDate = DateTime.UtcNow
                };
                dbContext.Employee.Add(superAdminEmployee);
                await dbContext.SaveChangesAsync();
            }
            else if (superAdminEmployee.BranchId != mainBranch.BranchId)
            {
                // Move SuperAdmin to Main Branch if not already there
                superAdminEmployee.BranchId = mainBranch.BranchId;
                await dbContext.SaveChangesAsync();
            }

            // Update user's BranchId
            superAdminUser.BranchId = mainBranch.BranchId;
            await userManager.UpdateAsync(superAdminUser);

            // Ensure SuperAdmin has dynamic role assigned
            var superAdminRole = await dbContext.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == "SuperAdmin");
            if (superAdminRole != null)
            {
                var hasRole = await dbContext.DynamicUserRoles.AnyAsync(ur => 
                    ur.UserId == superAdminUser.Id && ur.RoleId == superAdminRole.RoleId && ur.IsActive);
                
                if (!hasRole)
                {
                    await permissionService.AssignRoleToUserAsync(superAdminUser.Id, superAdminRole.RoleId, "System");
                }
            }
        }
    }
}
