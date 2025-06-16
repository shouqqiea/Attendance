using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LetsCheckIn.Models.db;

namespace LetsCheckIn.Models
{
    public interface IBranchAccessService
    {
        Task<bool> CanAccessBranchAsync(string userId, int branchId);
        Task<bool> IsSuperAdminAsync(string userId);
        Task<List<int>> GetAccessibleBranchIdsAsync(string userId);
        Task<Branch?> GetUserBranchAsync(string userId);
        Task<bool> CanAssignRoleAsync(string assignerUserId, string targetRole, int targetBranchId);
        Task<bool> CanCreateBranchAsync(string userId);
    }

    public class BranchAccessService : IBranchAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BranchAccessService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> CanAccessBranchAsync(string userId, int branchId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            var userRoles = await _userManager.GetRolesAsync(user);

            // SuperAdmin can access all branches if they're on the superadmin branch
            if (userRoles.Contains("SuperAdmin"))
            {
                return await IsSuperAdminAsync(userId);
            }

            // Admin can access their own branch and create new branches
            if (userRoles.Contains("Admin"))
            {
                var employee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.UserId == userId);
                
                if (employee == null) return false;

                // Admin can access their own branch
                if (employee.BranchId == branchId) return true;

                // Check if this is a new branch being created by the admin
                var branch = await _context.Branch.FindAsync(branchId);
                if (branch == null) return true; // Allow access for new branch creation
                
                return false;
            }

            // Other roles can only access their assigned branch
            var userEmployee = await _context.Employee
                .FirstOrDefaultAsync(e => e.UserId == userId);
            return userEmployee?.BranchId == branchId;
        }

        public async Task<bool> IsSuperAdminAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains("SuperAdmin")) return false;

            // Check if user is on the superadmin branch
            var employee = await _context.Employee
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.UserId == userId);

            return employee?.Branch?.IsSuperAdminBranch == true;
        }

        public async Task<List<int>> GetAccessibleBranchIdsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return new List<int>();

            var userRoles = await _userManager.GetRolesAsync(user);

            // SuperAdmin on superadmin branch can access all branches
            if (userRoles.Contains("SuperAdmin") && await IsSuperAdminAsync(userId))
            {
                return await _context.Branch
                    .Where(b => b.DeletedDate == null)
                    .Select(b => b.BranchId)
                    .ToListAsync();
            }

            // Admin can access their own branch
            if (userRoles.Contains("Admin"))
            {
                var employee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.UserId == userId);
                
                if (employee != null)
                {
                    return new List<int> { employee.BranchId };
                }
            }

            // Other roles can only access their assigned branch
            var userEmployee = await _context.Employee
                .FirstOrDefaultAsync(e => e.UserId == userId);

            if (userEmployee != null)
            {
                return new List<int> { userEmployee.BranchId };
            }

            return new List<int>();
        }

        public async Task<Branch?> GetUserBranchAsync(string userId)
        {
            var employee = await _context.Employee
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.UserId == userId);

            return employee?.Branch;
        }

        public async Task<bool> CanAssignRoleAsync(string assignerUserId, string targetRole, int targetBranchId)
        {
            var assignerUser = await _userManager.FindByIdAsync(assignerUserId);
            if (assignerUser == null) return false;

            var assignerRoles = await _userManager.GetRolesAsync(assignerUser);

            // SuperAdmin can assign any role except SuperAdmin to non-superadmin branches
            if (assignerRoles.Contains("SuperAdmin") && await IsSuperAdminAsync(assignerUserId))
            {
                if (targetRole == "SuperAdmin")
                {
                    // SuperAdmin role can only be assigned to users on the superadmin branch
                    var targetBranch = await _context.Branch.FindAsync(targetBranchId);
                    return targetBranch?.IsSuperAdminBranch == true;
                }
                return true;
            }

            // Admin can assign Employee and Manager roles to their own branch only
            if (assignerRoles.Contains("Admin"))
            {
                var assignerEmployee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.UserId == assignerUserId);

                if (assignerEmployee?.BranchId != targetBranchId) return false;

                // Admin can only assign Employee and Manager roles
                return targetRole == "Employee" || targetRole == "Manager";
            }

            return false;
        }

        public async Task<bool> CanCreateBranchAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            var userRoles = await _userManager.GetRolesAsync(user);

            // SuperAdmin can always create branches
            if (userRoles.Contains("SuperAdmin") && await IsSuperAdminAsync(userId))
            {
                return true;
            }

            // Admin can create branches
            if (userRoles.Contains("Admin"))
            {
                return true;
            }

            return false;
        }
    }
}