using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LetsCheckIn.Models.db;
using LetsCheckIn.Helpers;

namespace LetsCheckIn.Models
{
    /// <summary>
    /// Interface for branch access control with hierarchical support
    /// </summary>
    public interface IBranchAccessService
    {
        Task<bool> CanAccessBranchAsync(string userId, int branchId);
        Task<bool> IsSuperAdminAsync(string userId);
        Task<List<int>> GetAccessibleBranchIdsAsync(string userId);
        Task<Branch?> GetUserBranchAsync(string userId);
        Task<bool> CanAssignRoleAsync(string assignerUserId, string targetRole, int targetBranchId);
        Task<bool> CanCreateBranchAsync(string userId);

        /// <summary>
        /// Gets all child branch IDs for a given branch (recursive for Corporate branches)
        /// </summary>
        /// <param name="branchId">Parent branch ID</param>
        /// <returns>List of child branch IDs</returns>
        Task<List<int>> GetChildBranchIdsAsync(int branchId);

        /// <summary>
        /// Gets the complete branch hierarchy starting from a root branch
        /// </summary>
        /// <param name="rootBranchId">Root branch ID to start from</param>
        /// <returns>Hierarchical tree of branches</returns>
        Task<List<BranchHierarchyNode>> GetBranchHierarchyAsync(int rootBranchId);

        /// <summary>
        /// Checks if a user can create a child branch under a specific parent branch
        /// </summary>
        /// <param name="userId">User attempting to create the branch</param>
        /// <param name="parentBranchId">Parent branch ID (null for root branches)</param>
        /// <returns>True if user can create child branch</returns>
        Task<bool> CanCreateChildBranchAsync(string userId, int? parentBranchId);

        /// <summary>
        /// Gets all descendant branch IDs for Corporate branches (includes children, grandchildren, etc.)
        /// </summary>
        /// <param name="branchId">Corporate branch ID</param>
        /// <returns>List of all descendant branch IDs</returns>
        Task<List<int>> GetDescendantBranchIdsAsync(int branchId);

        /// <summary>
        /// Validates branch type compatibility for hierarchy operations
        /// </summary>
        /// <param name="parentBranchId">Parent branch ID</param>
        /// <param name="childBranchType">Type of child branch being created</param>
        /// <returns>True if hierarchy is valid</returns>
        Task<bool> IsValidBranchHierarchyAsync(int? parentBranchId, int childBranchType);

        /// <summary>
        /// Gets the root branch for a given branch in the hierarchy
        /// </summary>
        /// <param name="branchId">Branch ID to find root for</param>
        /// <returns>Root branch</returns>
        Task<Branch?> GetRootBranchAsync(int branchId);
    }

    /// <summary>
    /// Represents a node in the branch hierarchy tree
    /// </summary>
    public class BranchHierarchyNode
    {
        public Branch Branch { get; set; } = null!;
        public List<BranchHierarchyNode> Children { get; set; } = new List<BranchHierarchyNode>();
    }

    /// <summary>
    /// Service for managing branch access control with hierarchical support
    /// </summary>
    public class BranchAccessService : IBranchAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly LetsCheckIn.Helpers.IDynamicPermissionService _permissionService;

        public BranchAccessService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, LetsCheckIn.Helpers.IDynamicPermissionService permissionService)
        {
            _context = context;
            _userManager = userManager;
            _permissionService = permissionService;
        }

        /// <summary>
        /// Checks if a user can access a specific branch based on hierarchy rules
        /// - SuperAdmin (Main Branch): Can access all branches
        /// - Corporate Branch users: Can access own branch + all descendant branches
        /// - Single Branch users: Can only access own branch
        /// </summary>
        public async Task<bool> CanAccessBranchAsync(string userId, int branchId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            // Get user roles using dynamic permission service
            var userRoles = await _permissionService.GetUserRolesAsync(userId);
            var userRoleNames = userRoles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // SuperAdmin can access all branches if they're on the superadmin branch
            if (userRoleNames.Contains("SuperAdmin"))
            {
                return await IsSuperAdminAsync(userId);
            }

            // Get user's branch information
            var employee = await _context.Employee
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.UserId == userId);
            
            if (employee == null) return false;

            var userBranch = employee.Branch;
            if (userBranch == null) return false;

            // Check if user is trying to access their own branch
            if (employee.BranchId == branchId) return true;

            // For Corporate branches, check if target branch is a descendant
            if (userBranch.IsCorporateBranch)
            {
                var descendantBranchIds = await GetDescendantBranchIdsAsync(employee.BranchId);
                return descendantBranchIds.Contains(branchId);
            }

            // For Single branches, users can only access their own branch
            // For new branch creation by Admin, allow access
            if (userRoleNames.Contains("Admin"))
            {
                var targetBranch = await _context.Branch.FindAsync(branchId);
                if (targetBranch == null) return true; // Allow access for new branch creation
            }

            return false;
        }

        /// <summary>
        /// Checks if a user is a SuperAdmin on the Main Branch
        /// </summary>
        public async Task<bool> IsSuperAdminAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) 
            {
                Console.WriteLine($"IsSuperAdminAsync - User not found for ID: {userId}");
                return false;
            }

            // Check if user has SuperAdmin role
            var userRoles = await _permissionService.GetUserRolesAsync(userId);
            var userRoleNames = userRoles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"IsSuperAdminAsync - User: {user.Email}, Roles: [{string.Join(", ", userRoleNames)}]");
            
            if (!userRoleNames.Contains("SuperAdmin")) 
            {
                Console.WriteLine($"IsSuperAdminAsync - User does not have SuperAdmin role");
                return false;
            }

            // Check if user is on the superadmin branch
            var employee = await _context.Employee
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.UserId == userId);

            var result = employee?.Branch?.IsSuperAdminBranch == true;
            Console.WriteLine($"IsSuperAdminAsync - Employee found: {employee != null}, Branch found: {employee?.Branch != null}, IsSuperAdminBranch: {employee?.Branch?.IsSuperAdminBranch}, Result: {result}");
            if (employee?.Branch != null)
            {
                Console.WriteLine($"IsSuperAdminAsync - Branch details: ID={employee.Branch.BranchId}, Name={employee.Branch.BranchName}, IsSuperAdminBranch={employee.Branch.IsSuperAdminBranch}");
            }

            return result;
        }

        /// <summary>
        /// Gets all accessible branch IDs for a user based on hierarchy rules
        /// </summary>
        public async Task<List<int>> GetAccessibleBranchIdsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) 
            {
                Console.WriteLine($"GetAccessibleBranchIdsAsync - User not found for ID: {userId}");
                return new List<int>();
            }

            // Get user roles
            var userRoles = await _permissionService.GetUserRolesAsync(userId);
            var userRoleNames = userRoles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"GetAccessibleBranchIdsAsync - User: {user.Email}, Roles: [{string.Join(", ", userRoleNames)}]");

            // SuperAdmin on superadmin branch can access all branches
            if (userRoleNames.Contains("SuperAdmin") && await IsSuperAdminAsync(userId))
            {
                var allBranches = await _context.Branch
                    .Where(b => b.DeletedDate == null)
                    .Select(b => b.BranchId)
                    .ToListAsync();
                Console.WriteLine($"GetAccessibleBranchIdsAsync - SuperAdmin access granted. Returning {allBranches.Count} branches: [{string.Join(", ", allBranches)}]");
                return allBranches;
            }

            // Get user's branch
            var employee = await _context.Employee
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.UserId == userId);
            
            if (employee?.Branch == null) return new List<int>();

            var accessibleBranchIds = new List<int> { employee.BranchId };

            // For Corporate branches, add all descendant branches
            if (employee.Branch.IsCorporateBranch)
            {
                var descendantIds = await GetDescendantBranchIdsAsync(employee.BranchId);
                accessibleBranchIds.AddRange(descendantIds);
            }

            return accessibleBranchIds.Distinct().ToList();
        }

        /// <summary>
        /// Gets the user's assigned branch
        /// </summary>
        public async Task<Branch?> GetUserBranchAsync(string userId)
        {
            var employee = await _context.Employee
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.UserId == userId);

            return employee?.Branch;
        }

        /// <summary>
        /// Checks if a user can assign a specific role to a target branch based on hierarchy rules
        /// </summary>
        public async Task<bool> CanAssignRoleAsync(string assignerUserId, string targetRole, int targetBranchId)
        {
            var assignerUser = await _userManager.FindByIdAsync(assignerUserId);
            if (assignerUser == null) return false;

            // Get assigner roles
            var assignerRoles = await _permissionService.GetUserRolesAsync(assignerUserId);
            var assignerRoleNames = assignerRoles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // SuperAdmin can assign any role except SuperAdmin to non-superadmin branches
            if (assignerRoleNames.Contains("SuperAdmin") && await IsSuperAdminAsync(assignerUserId))
            {
                if (targetRole == "SuperAdmin")
                {
                    // SuperAdmin role can only be assigned to users on the superadmin branch
                    var targetBranch = await _context.Branch.FindAsync(targetBranchId);
                    return targetBranch?.IsSuperAdminBranch == true;
                }
                return true;
            }

            // Admin can assign roles based on hierarchy access
            if (assignerRoleNames.Contains("Admin"))
            {
                // Check if assigner can access the target branch
                if (!await CanAccessBranchAsync(assignerUserId, targetBranchId)) return false;

                // Admin can only assign Employee and Manager roles
                return targetRole == "Employee" || targetRole == "Manager";
            }

            return false;
        }

        /// <summary>
        /// Checks if a user can create a branch based on their role and branch type
        /// </summary>
        public async Task<bool> CanCreateBranchAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            // Get user roles
            var userRoles = await _permissionService.GetUserRolesAsync(userId);
            var userRoleNames = userRoles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // SuperAdmin can always create branches
            if (userRoleNames.Contains("SuperAdmin") && await IsSuperAdminAsync(userId))
            {
                return true;
            }

            // Admin can create branches only if they're on a Corporate branch
            if (userRoleNames.Contains("Admin"))
            {
                var employee = await _context.Employee
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.UserId == userId);

                // Admin on Corporate branch can create child branches
                return employee?.Branch?.IsCorporateBranch == true;
            }

            return false;
        }

        #region Hierarchy-Specific Methods

        /// <summary>
        /// Gets immediate child branch IDs for a given parent branch
        /// </summary>
        public async Task<List<int>> GetChildBranchIdsAsync(int branchId)
        {
            return await _context.Branch
                .Where(b => b.ParentBranchId == branchId && b.DeletedDate == null)
                .Select(b => b.BranchId)
                .ToListAsync();
        }

        /// <summary>
        /// Gets all descendant branch IDs recursively (children, grandchildren, etc.)
        /// </summary>
        public async Task<List<int>> GetDescendantBranchIdsAsync(int branchId)
        {
            var descendants = new List<int>();
            var childIds = await GetChildBranchIdsAsync(branchId);
            
            descendants.AddRange(childIds);

            // Recursively get descendants of each child
            foreach (var childId in childIds)
            {
                var grandchildren = await GetDescendantBranchIdsAsync(childId);
                descendants.AddRange(grandchildren);
            }

            return descendants.Distinct().ToList();
        }

        /// <summary>
        /// Builds a hierarchical tree structure starting from a root branch
        /// </summary>
        public async Task<List<BranchHierarchyNode>> GetBranchHierarchyAsync(int rootBranchId)
        {
            var rootBranch = await _context.Branch
                .FirstOrDefaultAsync(b => b.BranchId == rootBranchId && b.DeletedDate == null);

            if (rootBranch == null) return new List<BranchHierarchyNode>();

            var rootNode = new BranchHierarchyNode { Branch = rootBranch };
            await PopulateChildrenAsync(rootNode);

            return new List<BranchHierarchyNode> { rootNode };
        }

        /// <summary>
        /// Recursively populates children for a hierarchy node
        /// </summary>
        private async Task PopulateChildrenAsync(BranchHierarchyNode node)
        {
            var childBranches = await _context.Branch
                .Where(b => b.ParentBranchId == node.Branch.BranchId && b.DeletedDate == null)
                .ToListAsync();

            foreach (var child in childBranches)
            {
                var childNode = new BranchHierarchyNode { Branch = child };
                node.Children.Add(childNode);
                await PopulateChildrenAsync(childNode);
            }
        }

        /// <summary>
        /// Checks if a user can create a child branch under a specific parent
        /// </summary>
        public async Task<bool> CanCreateChildBranchAsync(string userId, int? parentBranchId)
        {
            // Check basic create branch permission
            if (!await CanCreateBranchAsync(userId)) return false;

            // If no parent specified (root branch), only SuperAdmin can create
            if (parentBranchId == null)
            {
                return await IsSuperAdminAsync(userId);
            }

            // Check if user can access the parent branch
            if (!await CanAccessBranchAsync(userId, parentBranchId.Value)) return false;

            // Check if parent branch is Corporate type (can have children)
            var parentBranch = await _context.Branch.FindAsync(parentBranchId.Value);
            if (parentBranch == null || !parentBranch.IsCorporateBranch) return false;

            return true;
        }

        /// <summary>
        /// Validates if a branch hierarchy configuration is valid
        /// </summary>
        public async Task<bool> IsValidBranchHierarchyAsync(int? parentBranchId, int childBranchType)
        {
            // Root branches can be any type
            if (parentBranchId == null) return true;

            var parentBranch = await _context.Branch.FindAsync(parentBranchId.Value);
            if (parentBranch == null) return false;

            // Only Corporate branches can have children
            if (!parentBranch.IsCorporateBranch) return false;

            // Child branch type must be valid (1 = Corporate, 2 = Single)
            return childBranchType == 1 || childBranchType == 2;
        }

        /// <summary>
        /// Gets the root branch for a given branch in the hierarchy
        /// </summary>
        public async Task<Branch?> GetRootBranchAsync(int branchId)
        {
            var branch = await _context.Branch.FindAsync(branchId);
            if (branch == null) return null;

            // If this is already a root branch, return it
            if (branch.ParentBranchId == null) return branch;

            // Recursively find the root
            return await GetRootBranchAsync(branch.ParentBranchId.Value);
        }

        #endregion
    }
}