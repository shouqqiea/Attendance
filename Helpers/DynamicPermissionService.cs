using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LetsCheckIn.Helpers
{
    public interface IDynamicPermissionService
    {
        Task<bool> HasPermissionAsync(string userId, string permissionName);
        Task<bool> HasAnyPermissionAsync(string userId, params string[] permissions);
        Task<List<string>> GetUserPermissionsAsync(string userId);
        Task<List<Role>> GetUserRolesAsync(string userId);
        Task<List<Role>> GetAvailableRolesAsync(string userId, int? targetBranchId = null);
        Task<List<Role>> GetRolesForBranchAsync(int branchId);
        Task<List<Role>> GetUnassignedRolesForBranchAsync(int branchId);
        Task<bool> CanManageRoleAsync(string userId, int roleId);
        Task<bool> AssignRoleToUserAsync(string userId, int roleId, string assignedBy);
        Task<bool> RemoveRoleFromUserAsync(string userId, int roleId);
        Task<bool> AssignRoleToBranchAsync(int roleId, int branchId, string assignedBy);
        Task<bool> RemoveRoleFromBranchAsync(int roleId, int branchId);
        Task<List<Permission>> GetRolePermissionsAsync(int roleId);
        Task<bool> AssignPermissionToRoleAsync(int roleId, int permissionId, string assignedBy);
        Task<bool> RemovePermissionFromRoleAsync(int roleId, int permissionId);
        Task<Role?> CreateRoleAsync(string roleName, string? description, string createdBy);
        Task<bool> UpdateRoleAsync(int roleId, string roleName, string? description, string updatedBy);
        Task<bool> DeleteRoleAsync(int roleId);
        Task<List<Permission>> GetAllPermissionsAsync();
        Task SeedDefaultPermissionsAsync();
        Task MigrateExistingRolesAsync();
        Task MigrateExistingUsersAsync();
        Task<bool> UnassignRoleFromBranchAsync(int roleId, int branchId);
    }

    public class DynamicPermissionService : IDynamicPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DynamicPermissionService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> HasPermissionAsync(string userId, string permissionName)
        {
            var userRoles = await _context.DynamicUserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Include(ur => ur.Role)
                .Where(ur => ur.Role.IsActive)
                .Select(ur => ur.Role)
                .ToListAsync();

            if (!userRoles.Any())
                return false;

            var roleIds = userRoles.Select(r => r.RoleId).ToList();

            var hasPermission = await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Include(rp => rp.Permission)
                .AnyAsync(rp => rp.Permission.PermissionName == permissionName && rp.Permission.IsActive);

            return hasPermission;
        }

        public async Task<bool> HasAnyPermissionAsync(string userId, params string[] permissions)
        {
            var userRoles = await _context.DynamicUserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Include(ur => ur.Role)
                .Where(ur => ur.Role.IsActive)
                .Select(ur => ur.Role)
                .ToListAsync();

            if (!userRoles.Any())
                return false;

            var roleIds = userRoles.Select(r => r.RoleId).ToList();

            var hasAnyPermission = await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Include(rp => rp.Permission)
                .AnyAsync(rp => permissions.Contains(rp.Permission.PermissionName) && rp.Permission.IsActive);

            return hasAnyPermission;
        }

        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            var userRoles = await _context.DynamicUserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Include(ur => ur.Role)
                .Where(ur => ur.Role.IsActive)
                .Select(ur => ur.Role)
                .ToListAsync();

            if (!userRoles.Any())
                return new List<string>();

            var roleIds = userRoles.Select(r => r.RoleId).ToList();

            var permissions = await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Include(rp => rp.Permission)
                .Where(rp => rp.Permission.IsActive)
                .Select(rp => rp.Permission.PermissionName)
                .Distinct()
                .ToListAsync();

            return permissions;
        }

        public async Task<List<Role>> GetUserRolesAsync(string userId)
        {
            return await _context.DynamicUserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Include(ur => ur.Role)
                .Where(ur => ur.Role.IsActive)
                .Select(ur => ur.Role)
                .ToListAsync();
        }

        public async Task<List<Role>> GetAvailableRolesAsync(string userId, int? targetBranchId = null)
        {
            var query = _context.DynamicRoles.AsQueryable();

            if (targetBranchId.HasValue)
            {
                query = query.Where(r => _context.BranchRoles
                    .Any(br => br.RoleId == r.RoleId && br.BranchId == targetBranchId));
            }

            var assignedRoleIds = await _context.DynamicUserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            return await query
                .Where(r => !assignedRoleIds.Contains(r.RoleId) && r.IsActive)
                .ToListAsync();
        }

        public async Task<List<Role>> GetRolesForBranchAsync(int branchId)
        {
            return await _context.BranchRoles
                .Where(br => br.BranchId == branchId)
                .Include(br => br.Role)
                .Where(br => br.Role.IsActive)
                .Select(br => br.Role)
                .ToListAsync();
        }

        public async Task<List<Role>> GetUnassignedRolesForBranchAsync(int branchId)
        {
            var assignedRoleIds = await _context.BranchRoles
                .Where(br => br.BranchId == branchId)
                .Select(br => br.RoleId)
                .ToListAsync();

            return await _context.DynamicRoles
                .Where(r => !assignedRoleIds.Contains(r.RoleId) && r.IsActive)
                .ToListAsync();
        }

        public async Task<bool> CanManageRoleAsync(string userId, int roleId)
        {
            var userRoles = await _context.DynamicUserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Select(ur => ur.Role)
                .ToListAsync();

            // SuperAdmin can manage all roles
            if (userRoles.Any(r => r.RoleName == "SuperAdmin"))
            {
                return true;
            }

            // Admin can manage non-system roles
            if (userRoles.Any(r => r.RoleName == "Admin"))
            {
                var role = await _context.DynamicRoles.FindAsync(roleId);
                return role != null && !role.IsSystemRole;
            }

            return false;
        }

        public async Task<bool> AssignRoleToUserAsync(string userId, int roleId, string assignedBy)
        {
            try
            {
                // ✅ Verify role exists and is active
                var role = await _context.DynamicRoles
                    .FirstOrDefaultAsync(r => r.RoleId == roleId && r.IsActive && r.DeletedDate == null);
                
                if (role == null)
                {
                    return false;
                }

                // ✅ Verify user exists
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                var existingAssignment = await _context.DynamicUserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                if (existingAssignment != null)
                {
                    if (!existingAssignment.IsActive)
                    {
                        existingAssignment.IsActive = true;
                        existingAssignment.AssignedDate = DateTime.UtcNow;
                        existingAssignment.AssignedBy = assignedBy;
                        await _context.SaveChangesAsync();
                    }
                    return true;
                }

                var userRole = new DynamicUserRole
                {
                    UserId = userId,
                    RoleId = roleId,
                    AssignedBy = assignedBy,
                    AssignedDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.DynamicUserRoles.Add(userRole);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> RemoveRoleFromUserAsync(string userId, int roleId)
        {
            try
            {
                // ✅ Verify role exists
                var role = await _context.DynamicRoles
                    .FirstOrDefaultAsync(r => r.RoleId == roleId);
                
                if (role == null)
                {
                    return false;
                }

                // ✅ Verify user exists
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                var userRole = await _context.DynamicUserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.IsActive);

                if (userRole != null)
                {
                    userRole.IsActive = false;
                    await _context.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> AssignRoleToBranchAsync(int roleId, int branchId, string assignedBy)
        {
            try
            {
                // Check for existing active assignment
                var existingAssignment = await _context.BranchRoles
                    .FirstOrDefaultAsync(br => br.RoleId == roleId && br.BranchId == branchId);

                if (existingAssignment != null)
                {
                    if (existingAssignment.IsActive)
                    {
                        // Already assigned and active
                        return false;
                    }
                    else
                    {
                        // Reactivate the existing assignment
                        existingAssignment.IsActive = true;
                        existingAssignment.AssignedBy = assignedBy;
                        existingAssignment.AssignedDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        return true;
                    }
                }

                // Create new assignment
                var branchRole = new BranchRole
                {
                    RoleId = roleId,
                    BranchId = branchId,
                    AssignedBy = assignedBy,
                    AssignedDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.BranchRoles.Add(branchRole);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Log the exception for debugging - using System.Diagnostics for now
                System.Diagnostics.Debug.WriteLine($"Error in AssignRoleToBranchAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> RemoveRoleFromBranchAsync(int roleId, int branchId)
        {
            try
            {
                var branchRole = await _context.BranchRoles
                    .FirstOrDefaultAsync(br => br.RoleId == roleId && br.BranchId == branchId);

                if (branchRole != null)
                {
                    branchRole.IsActive = false;
                    await _context.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<List<Permission>> GetRolePermissionsAsync(int roleId)
        {
            return await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Include(rp => rp.Permission)
                .Where(rp => rp.Permission.IsActive)
                .Select(rp => rp.Permission)
                .ToListAsync();
        }

        public async Task<bool> AssignPermissionToRoleAsync(int roleId, int permissionId, string assignedBy)
        {
            try
            {
                var existingAssignment = await _context.RolePermissions
                    .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

                if (existingAssignment != null)
                {
                    if (!existingAssignment.IsActive)
                    {
                        existingAssignment.IsActive = true;
                        existingAssignment.AssignedDate = DateTime.UtcNow;
                        existingAssignment.AssignedBy = assignedBy;
                        await _context.SaveChangesAsync();
                    }
                    return true;
                }

                var rolePermission = new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId,
                    AssignedBy = assignedBy,
                    AssignedDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.RolePermissions.Add(rolePermission);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> RemovePermissionFromRoleAsync(int roleId, int permissionId)
        {
            try
            {
                var rolePermission = await _context.RolePermissions
                    .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

                if (rolePermission != null)
                {
                    rolePermission.IsActive = false;
                    await _context.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<Role?> CreateRoleAsync(string roleName, string? description, string createdBy)
        {
            try
            {
                var role = new Role
                {
                    RoleName = roleName,
                    Description = description,
                    IsSystemRole = false,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = createdBy
                };

                _context.DynamicRoles.Add(role);
                await _context.SaveChangesAsync();
                return role;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> UpdateRoleAsync(int roleId, string roleName, string? description, string updatedBy)
        {
            try
            {
                var role = await _context.DynamicRoles.FindAsync(roleId);
                if (role == null) return false;

                role.RoleName = roleName;
                role.Description = description;
                role.UpdatedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeleteRoleAsync(int roleId)
        {
            try
            {
                var role = await _context.DynamicRoles.FindAsync(roleId);
                if (role == null || role.IsSystemRole) return false;

                role.DeletedDate = DateTime.UtcNow;
                role.IsActive = false;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            return await _context.Permissions
                .Where(p => p.IsActive)
                .OrderBy(p => p.Category)
                .ThenBy(p => p.PermissionName)
                .ToListAsync();
        }

        public async Task SeedDefaultPermissionsAsync()
        {
            var permissions = new List<Permission>
            {
                // Dashboard Management
                new Permission { PermissionName = "dashboard.view", Description = "View dashboard", Category = "Dashboard Management" },
                new Permission { PermissionName = "dashboard.analytics", Description = "View dashboard analytics", Category = "Dashboard Management" },
                new Permission { PermissionName = "dashboard.reports", Description = "View dashboard reports", Category = "Dashboard Management" },
                new Permission { PermissionName = "dashboard.export", Description = "Export dashboard data", Category = "Dashboard Management" },
                
                // User Management
                new Permission { PermissionName = "user.view", Description = "View users", Category = "User Management" },
                new Permission { PermissionName = "user.create", Description = "Create users", Category = "User Management" },
                new Permission { PermissionName = "user.edit", Description = "Edit users", Category = "User Management" },
                new Permission { PermissionName = "user.delete", Description = "Delete users", Category = "User Management" },
                
                // Role Management
                new Permission { PermissionName = "role.view", Description = "View roles", Category = "Role Management" },
                new Permission { PermissionName = "role.create", Description = "Create roles", Category = "Role Management" },
                new Permission { PermissionName = "role.edit", Description = "Edit roles", Category = "Role Management" },
                new Permission { PermissionName = "role.delete", Description = "Delete roles", Category = "Role Management" },
                new Permission { PermissionName = "role.assign", Description = "Assign roles to users", Category = "Role Management" },
                
                // Branch Management
                new Permission { PermissionName = "branch.view", Description = "View branches", Category = "Branch Management" },
                new Permission { PermissionName = "branch.create", Description = "Create branches", Category = "Branch Management" },
                new Permission { PermissionName = "branch.edit", Description = "Edit branches", Category = "Branch Management" },
                new Permission { PermissionName = "branch.delete", Description = "Delete branches", Category = "Branch Management" },
                
                // Leave Management
                new Permission { PermissionName = "leave.view", Description = "View leave requests", Category = "Leave Management" },
                new Permission { PermissionName = "leave.create", Description = "Create leave requests", Category = "Leave Management" },
                new Permission { PermissionName = "leave.approve", Description = "Approve leave requests", Category = "Leave Management" },
                new Permission { PermissionName = "leave.reject", Description = "Reject leave requests", Category = "Leave Management" },
                
                // System Administration
                new Permission { PermissionName = "system.admin", Description = "Full system administration", Category = "System Administration" }
            };

            foreach (var permission in permissions)
            {
                var exists = await _context.Permissions.AnyAsync(p => p.PermissionName == permission.PermissionName);
                if (!exists)
                {
                    _context.Permissions.Add(permission);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task MigrateExistingRolesAsync()
        {
            // Create default roles if they don't exist
            var defaultRoles = new List<(string Name, string Description, bool IsSystem, List<string> Permissions)>
            {
                ("SuperAdmin", "Full system administrator", true, new List<string> 
                { 
                    "system.admin",
                    "dashboard.view", "dashboard.analytics", "dashboard.reports", "dashboard.export",
                    "user.view", "user.create", "user.edit", "user.delete",
                    "role.view", "role.create", "role.edit", "role.delete", "role.assign",
                    "branch.view", "branch.create", "branch.edit", "branch.delete",
                    "leave.view", "leave.create", "leave.approve", "leave.reject"
                }),
                ("Admin", "Branch administrator", true, new List<string> 
                { 
                    "dashboard.view", "dashboard.analytics", "dashboard.reports", "dashboard.export",
                    "user.view", "user.create", "user.edit", "user.delete",
                    "branch.view", "branch.create", "branch.edit",
                    "leave.view", "leave.approve", "leave.reject"
                }),
                ("Manager", "Department manager", true, new List<string> 
                { 
                    "dashboard.view", "dashboard.analytics", "dashboard.reports", "dashboard.export",
                    "user.view", "user.create", "user.edit", "user.delete",
                    "leave.view", "leave.approve", "leave.reject"
                }),
                ("Employee", "Regular employee", true, new List<string> 
                { 
                    "dashboard.view", "dashboard.analytics", "dashboard.reports", "dashboard.export",
                    "leave.view", "leave.create"
                })
            };

            foreach (var (name, description, isSystem, permissionNames) in defaultRoles)
            {
                var existingRole = await _context.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == name);
                
                Role role;
                if (existingRole == null)
                {
                    role = new Role
                    {
                        RoleName = name,
                        Description = description,
                        IsSystemRole = isSystem,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    _context.DynamicRoles.Add(role);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    role = existingRole;
                    if (role.Description != description)
                    {
                        role.Description = description;
                        role.UpdatedDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                // For SuperAdmin, get all available permissions
                var permissionsToAssign = name == "SuperAdmin" 
                    ? await _context.Permissions.Where(p => p.IsActive).ToListAsync()
                    : await _context.Permissions
                        .Where(p => permissionNames.Contains(p.PermissionName) && p.IsActive)
                        .ToListAsync();

                foreach (var permission in permissionsToAssign)
                {
                    var existingPermission = await _context.RolePermissions
                        .FirstOrDefaultAsync(rp => rp.RoleId == role.RoleId && rp.PermissionId == permission.PermissionId);
                    
                    if (existingPermission == null)
                    {
                        await AssignPermissionToRoleAsync(role.RoleId, permission.PermissionId, "System");
                    }
                    else if (!existingPermission.IsActive)
                    {
                        existingPermission.IsActive = true;
                        existingPermission.AssignedDate = DateTime.UtcNow;
                        existingPermission.AssignedBy = "System";
                        await _context.SaveChangesAsync();
                    }
                }

                // Assign system roles to all branches (except SuperAdmin which doesn't need branch assignment)
                if (isSystem && name != "SuperAdmin")
                {
                    var allBranches = await _context.Branch.Where(b => b.DeletedDate == null).ToListAsync();
                    foreach (var branch in allBranches)
                    {
                        await AssignRoleToBranchAsync(role.RoleId, branch.BranchId, "System");
                    }
                }
            }
        }

        public async Task MigrateExistingUsersAsync()
        {
            // ✅ Ensure all users without dynamic roles get default Employee role
            var allUsers = await _userManager.Users
                .Include(u => u.Employee)
                .ToListAsync();

            foreach (var user in allUsers)
            {
                var existingDynamicRoles = await _context.DynamicUserRoles
                    .Where(ur => ur.UserId == user.Id && ur.IsActive)
                    .ToListAsync();

                // If user has no dynamic roles, assign default Employee role
                if (!existingDynamicRoles.Any())
                {
                    var employeeRole = await _context.DynamicRoles
                        .FirstOrDefaultAsync(r => r.RoleName == "Employee");
                    
                    if (employeeRole != null)
                    {
                        await AssignRoleToUserAsync(user.Id, employeeRole.RoleId, "Migration");
                    }
                }
            }
        }

        public async Task<bool> UnassignRoleFromBranchAsync(int roleId, int branchId)
        {
            try
            {
                var branchRole = await _context.BranchRoles
                    .FirstOrDefaultAsync(br => br.RoleId == roleId && br.BranchId == branchId);

                if (branchRole == null)
                    return false;

                _context.BranchRoles.Remove(branchRole);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
} 