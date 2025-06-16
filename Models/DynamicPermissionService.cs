using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LetsCheckIn.Models.db;
using Microsoft.Extensions.Logging;

namespace LetsCheckIn.Models
{
    public interface IDynamicPermissionService
    {
        Task<bool> HasPermissionAsync(string userId, string permissionName);
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
    }

    public class DynamicPermissionService : IDynamicPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBranchAccessService _branchAccessService;
        private readonly ILogger<DynamicPermissionService> _logger;

        public DynamicPermissionService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBranchAccessService branchAccessService,
            ILogger<DynamicPermissionService> logger)
        {
            _context = context;
            _userManager = userManager;
            _branchAccessService = branchAccessService;
            _logger = logger;
        }

        public async Task<bool> HasPermissionAsync(string userId, string permissionName)
        {
            var userPermissions = await GetUserPermissionsAsync(userId);
            return userPermissions.Contains(permissionName);
        }

        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            var permissions = await _context.DynamicUserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .Where(ur => ur.Role.IsActive && ur.Role.DeletedDate == null)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Where(rp => rp.IsActive && rp.Permission.IsActive)
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
                .Select(ur => ur.Role)
                .ToListAsync();
        }

        public async Task<List<Role>> GetAvailableRolesAsync(string userId, int? targetBranchId = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return new List<Role>();

            // Get accessible branch IDs for the user
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(userId);

            // SuperAdmin can see all roles
            if (await _branchAccessService.IsSuperAdminAsync(userId))
            {
                var allRoles = await _context.DynamicRoles
                    .Where(r => r.IsActive && r.DeletedDate == null)
                    .ToListAsync();
                
                _logger.LogInformation($"SuperAdmin access - Found {allRoles.Count} total roles:");
                foreach (var role in allRoles)
                {
                    _logger.LogInformation($"Role: {role.RoleName} (ID: {role.RoleId}) - IsSystem: {role.IsSystemRole}");
                }
                return allRoles;
            }

            // For specific branch, return roles assigned to that branch
            if (targetBranchId.HasValue && accessibleBranchIds.Contains(targetBranchId.Value))
            {
                var branchRoles = await GetRolesForBranchAsync(targetBranchId.Value);
                _logger.LogInformation($"Branch {targetBranchId} access - Found {branchRoles.Count} roles:");
                foreach (var role in branchRoles)
                {
                    _logger.LogInformation($"Role: {role.RoleName} (ID: {role.RoleId}) - IsSystem: {role.IsSystemRole}");
                }
                return branchRoles;
            }

            // For general access, return roles assigned to any accessible branch
            var rolesInAccessibleBranches = await _context.BranchRoles
                .Where(br => br.IsActive && accessibleBranchIds.Contains(br.BranchId))
                .Include(br => br.Role)
                .Where(br => br.Role.IsActive && br.Role.DeletedDate == null)
                .Select(br => br.Role)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation($"General access - Found {rolesInAccessibleBranches.Count} roles in accessible branches:");
            foreach (var role in rolesInAccessibleBranches)
            {
                _logger.LogInformation($"Role: {role.RoleName} (ID: {role.RoleId}) - IsSystem: {role.IsSystemRole}");
            }
            return rolesInAccessibleBranches;
        }

        public async Task<List<Role>> GetRolesForBranchAsync(int branchId)
        {
            return await _context.BranchRoles
                .Where(br => br.BranchId == branchId && br.IsActive)
                .Include(br => br.Role)
                .Where(br => br.Role.IsActive && br.Role.DeletedDate == null)
                .Select(br => br.Role)
                .ToListAsync();
        }

        public async Task<List<Role>> GetUnassignedRolesForBranchAsync(int branchId)
        {
            var assignedRoleIds = await _context.BranchRoles
                .Where(br => br.BranchId == branchId && br.IsActive)
                .Select(br => br.RoleId)
                .ToListAsync();

            return await _context.DynamicRoles
                .Where(r => r.IsActive && r.DeletedDate == null && !assignedRoleIds.Contains(r.RoleId))
                .ToListAsync();
        }

        public async Task<bool> CanManageRoleAsync(string userId, int roleId)
        {
            var role = await _context.DynamicRoles.FirstOrDefaultAsync(r => r.RoleId == roleId);
            if (role == null) return false;

            // SuperAdmin can manage all roles
            if (await _branchAccessService.IsSuperAdminAsync(userId))
            {
                return true;
            }

            // For system roles, allow user assignment/removal management by admins
            // but restrict other operations like role creation/deletion/modification
            if (role.IsSystemRole)
            {
                // Allow admins to manage user assignments to system roles
                // System roles should be manageable by any admin, not just SuperAdmins
                var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(userId);
                
                // If user has any accessible branches, allow system role management
                // This is because system roles are typically assigned to all branches
                if (accessibleBranchIds.Any())
                {
                    return true;
                }
                
                // Fallback: check if role is assigned to accessible branches
                var roleIsInAccessibleBranches = await _context.BranchRoles
                    .AnyAsync(br => br.RoleId == roleId && br.IsActive && accessibleBranchIds.Contains(br.BranchId));
                
                return roleIsInAccessibleBranches;
            }

            // For non-system roles, check if user has access to any branch where this role is assigned
            var accessibleBranchIds2 = await _branchAccessService.GetAccessibleBranchIdsAsync(userId);
            var roleIsInAccessibleBranches2 = await _context.BranchRoles
                .AnyAsync(br => br.RoleId == roleId && br.IsActive && accessibleBranchIds2.Contains(br.BranchId));

            return roleIsInAccessibleBranches2;
        }

        public async Task<bool> AssignRoleToUserAsync(string userId, int roleId, string assignedBy)
        {
            _logger.LogInformation($"Attempting to assign role {roleId} to user {userId} by {assignedBy}");
            
            // First try to find the role by ID
            var role = await _context.DynamicRoles
                .FirstOrDefaultAsync(r => r.RoleId == roleId && r.IsActive && r.DeletedDate == null);
            
            // If role not found by ID, try to find it by name (for backward compatibility)
            if (role == null)
            {
                _logger.LogWarning($"Role ID {roleId} not found, attempting to find by name");
                var roleName = await _context.DynamicRoles
                    .Where(r => r.RoleId == roleId)
                    .Select(r => r.RoleName)
                    .FirstOrDefaultAsync();
                
                if (roleName != null)
                {
                    role = await _context.DynamicRoles
                        .FirstOrDefaultAsync(r => r.RoleName == roleName && r.IsActive && r.DeletedDate == null);
                    
                    if (role != null)
                    {
                        _logger.LogInformation($"Found role by name: {role.RoleName} (ID: {role.RoleId})");
                        roleId = role.RoleId; // Update the roleId to the correct one
                    }
                }
            }
            
            if (role == null)
            {
                _logger.LogError($"Role {roleId} not found or not active");
                return false;
            }
            _logger.LogInformation($"Found role: {role.RoleName} (ID: {role.RoleId}) - IsSystem: {role.IsSystemRole}");
            
            // Verify the user exists
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogError($"User {userId} not found");
                return false;
            }
            _logger.LogInformation($"Found user: {user.Email} (ID: {user.Id})");
            
            // Check if user already has the Identity role
            var userRoles = await _userManager.GetRolesAsync(user);
            bool hasIdentityRole = userRoles.Contains(role.RoleName);
            
            // Only try to add the Identity role if the user doesn't already have it
            if (!hasIdentityRole)
            {
                var identityRoleResult = await _userManager.AddToRoleAsync(user, role.RoleName);
                if (!identityRoleResult.Succeeded)
                {
                    _logger.LogError($"Failed to assign Identity role: {string.Join(", ", identityRoleResult.Errors.Select(e => e.Description))}");
                    return false;
                }
                _logger.LogInformation($"Successfully assigned Identity role {role.RoleName}");
            }
            else
            {
                _logger.LogInformation($"User already has Identity role {role.RoleName}");
            }
            
            // Then assign the role in our Dynamic Roles system
            var existingAssignment = await _context.DynamicUserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

            if (existingAssignment != null)
            {
                _logger.LogInformation($"Found existing dynamic role assignment for user {userId} and role {roleId}");
                if (!existingAssignment.IsActive)
                {
                    _logger.LogInformation($"Reactivating inactive dynamic role assignment for user {userId} and role {roleId}");
                    existingAssignment.IsActive = true;
                    existingAssignment.AssignedDate = DateTime.UtcNow;
                    existingAssignment.AssignedBy = assignedBy;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully reactivated dynamic role assignment");
                }
                else
                {
                    _logger.LogInformation($"Dynamic role assignment already active for user {userId} and role {roleId}");
                }
                return true;
            }

            _logger.LogInformation($"Creating new dynamic role assignment for user {userId} and role {roleId}");
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
            
            // Verify the assignment was saved
            var savedAssignment = await _context.DynamicUserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            
            if (savedAssignment != null && savedAssignment.IsActive)
            {
                _logger.LogInformation($"Successfully created and verified new dynamic role assignment");
                return true;
            }
            else
            {
                _logger.LogError($"Failed to verify dynamic role assignment after save");
                // Only remove the Identity role if we added it in this call
                if (!hasIdentityRole)
                {
                    await _userManager.RemoveFromRoleAsync(user, role.RoleName);
                }
                return false;
            }
        }

        public async Task<bool> RemoveRoleFromUserAsync(string userId, int roleId)
        {
            _logger.LogInformation($"Attempting to remove role {roleId} from user {userId}");
            
            try
            {
                // Get the role name first
                var role = await _context.DynamicRoles
                    .FirstOrDefaultAsync(r => r.RoleId == roleId);
                
                if (role == null)
                {
                    _logger.LogError($"Role {roleId} not found");
                    return false;
                }
                
                // Get the user
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"User {userId} not found");
                    return false;
                }
                
                // Remove from ASP.NET Identity
                var identityRoleResult = await _userManager.RemoveFromRoleAsync(user, role.RoleName);
                if (!identityRoleResult.Succeeded)
                {
                    _logger.LogError($"Failed to remove Identity role: {string.Join(", ", identityRoleResult.Errors.Select(e => e.Description))}");
                    return false;
                }
                _logger.LogInformation($"Successfully removed Identity role {role.RoleName} from user {user.Email}");
                
                // Remove from Dynamic Roles
                var userRole = await _context.DynamicUserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.IsActive);

                if (userRole != null)
                {
                    userRole.IsActive = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully deactivated dynamic role assignment for user {user.Email}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"No active dynamic role assignment found to remove");
                    return true; // Return true since the Identity role was removed successfully
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in RemoveRoleFromUserAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> AssignRoleToBranchAsync(int roleId, int branchId, string assignedBy)
        {
            // Check if assignment already exists
            var existingAssignment = await _context.BranchRoles
                .FirstOrDefaultAsync(br => br.RoleId == roleId && br.BranchId == branchId);

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

            var branchRole = new BranchRole
            {
                RoleId = roleId,
                BranchId = branchId,
                AssignedBy = assignedBy,
                IsActive = true
            };

            _context.BranchRoles.Add(branchRole);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveRoleFromBranchAsync(int roleId, int branchId)
        {
            var branchRole = await _context.BranchRoles
                .FirstOrDefaultAsync(br => br.RoleId == roleId && br.BranchId == branchId && br.IsActive);

            if (branchRole != null)
            {
                branchRole.IsActive = false;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<List<Permission>> GetRolePermissionsAsync(int roleId)
        {
            return await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId && rp.IsActive)
                .Include(rp => rp.Permission)
                .Where(rp => rp.Permission.IsActive)
                .Select(rp => rp.Permission)
                .ToListAsync();
        }

        public async Task<bool> AssignPermissionToRoleAsync(int roleId, int permissionId, string assignedBy)
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
                IsActive = true
            };

            _context.RolePermissions.Add(rolePermission);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemovePermissionFromRoleAsync(int roleId, int permissionId)
        {
            var rolePermission = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId && rp.IsActive);

            if (rolePermission != null)
            {
                rolePermission.IsActive = false;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<Role?> CreateRoleAsync(string roleName, string? description, string createdBy)
        {
            var role = new Role
            {
                RoleName = roleName,
                Description = description,
                IsActive = true,
                IsSystemRole = false,
                CreatedDate = DateTime.UtcNow
            };

            _context.DynamicRoles.Add(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task<bool> UpdateRoleAsync(int roleId, string roleName, string? description, string updatedBy)
        {
            var role = await _context.DynamicRoles.FindAsync(roleId);
            if (role == null || role.IsSystemRole) return false;

            role.RoleName = roleName;
            role.Description = description;
            role.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRoleAsync(int roleId)
        {
            var role = await _context.DynamicRoles.FindAsync(roleId);
            if (role == null || role.IsSystemRole) return false;

            role.DeletedDate = DateTime.UtcNow;
            role.IsActive = false;

            // Deactivate all user role assignments
            var userRoles = await _context.DynamicUserRoles.Where(ur => ur.RoleId == roleId).ToListAsync();
            foreach (var userRole in userRoles)
            {
                userRole.IsActive = false;
            }

            // Deactivate all branch role assignments
            var branchRoles = await _context.BranchRoles.Where(br => br.RoleId == roleId).ToListAsync();
            foreach (var branchRole in branchRoles)
            {
                branchRole.IsActive = false;
            }

            await _context.SaveChangesAsync();
            return true;
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
                ("SuperAdmin", "Full system administrator", true, new List<string> { "system.admin" }),
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
                    "leave.view"
                })
            };

            foreach (var (name, description, isSystem, permissionNames) in defaultRoles)
            {
                var existingRole = await _context.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == name);
                
                Role role;
                if (existingRole == null)
                {
                    // Create new role
                    role = new Role
                    {
                        RoleName = name,
                        Description = description,
                        IsSystemRole = isSystem,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.DynamicRoles.Add(role);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Use existing role
                    role = existingRole;
                    
                    // Update description if it's different
                    if (role.Description != description)
                    {
                        role.Description = description;
                        role.UpdatedDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                // Assign permissions to role (both new and existing roles)
                foreach (var permissionName in permissionNames)
                {
                    var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.PermissionName == permissionName);
                    if (permission != null)
                    {
                        // Check if permission is already assigned
                        var existingPermission = await _context.RolePermissions
                            .FirstOrDefaultAsync(rp => rp.RoleId == role.RoleId && rp.PermissionId == permission.PermissionId);
                        
                        if (existingPermission == null)
                        {
                            // Assign new permission
                            await AssignPermissionToRoleAsync(role.RoleId, permission.PermissionId, "System");
                        }
                        else if (!existingPermission.IsActive)
                        {
                            // Reactivate inactive permission
                            existingPermission.IsActive = true;
                            existingPermission.AssignedDate = DateTime.UtcNow;
                            existingPermission.AssignedBy = "System";
                            await _context.SaveChangesAsync();
                        }
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
            // Get all users who have ASP.NET Identity roles but no dynamic roles
            var allUsers = await _userManager.Users
                .Include(u => u.Employee)
                .ToListAsync();

            foreach (var user in allUsers)
            {
                // Get user's ASP.NET Identity roles
                var identityRoles = await _userManager.GetRolesAsync(user);
                
                // Check if user has any dynamic roles
                var existingDynamicRoles = await _context.DynamicUserRoles
                    .Where(ur => ur.UserId == user.Id && ur.IsActive)
                    .ToListAsync();

                // If user has identity roles but no dynamic roles, migrate them
                if (identityRoles.Any() && !existingDynamicRoles.Any())
                {
                    foreach (var roleName in identityRoles)
                    {
                        var dynamicRole = await _context.DynamicRoles
                            .FirstOrDefaultAsync(r => r.RoleName == roleName);
                        
                        if (dynamicRole != null)
                        {
                            await AssignRoleToUserAsync(user.Id, dynamicRole.RoleId, "Migration");
                        }
                    }
                }
            }
        }
    }
} 