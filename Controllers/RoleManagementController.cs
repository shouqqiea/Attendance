using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using System.ComponentModel.DataAnnotations;
using LetsCheckIn.Helpers;

namespace LetsCheckIn.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class RoleManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly LetsCheckIn.Helpers.IDynamicPermissionService _permissionService;
        private readonly IBranchAccessService _branchAccessService;
        private readonly ILogger<RoleManagementController> _logger;

        public RoleManagementController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            LetsCheckIn.Helpers.IDynamicPermissionService permissionService,
            IBranchAccessService branchAccessService,
            ILogger<RoleManagementController> logger)
        {
            _context = context;
            _userManager = userManager;
            _permissionService = permissionService;
            _branchAccessService = branchAccessService;
            _logger = logger;
        }

        // GET: /RoleManagement
        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                var roles = await _permissionService.GetAvailableRolesAsync(currentUser.Id);
                
                var roleViewModels = new List<RoleViewModel>();
                
                foreach (var role in roles)
                {
                    _logger.LogInformation($"Processing role {role.RoleName} (ID: {role.RoleId})");
                    
                    // Get branches where this role is assigned
                    var branchAssignments = await _context.BranchRoles
                        .Where(br => br.RoleId == role.RoleId && br.IsActive)
                        .Include(br => br.Branch)
                        .ToListAsync();
                    
                    // Get all user assignments for this role
                    var userRoles = await _context.DynamicUserRoles
                        .Where(ur => ur.RoleId == role.RoleId)
                        .Include(ur => ur.User)
                        .ToListAsync();
                    
                    _logger.LogInformation($"Role {role.RoleName} (ID: {role.RoleId}) has {userRoles.Count} total user assignments");
                    _logger.LogInformation($"Active assignments: {userRoles.Count(ur => ur.IsActive)}");
                    _logger.LogInformation($"Inactive assignments: {userRoles.Count(ur => !ur.IsActive)}");
                    
                    // Log details of each assignment
                    foreach (var userRole in userRoles)
                    {
                        _logger.LogInformation($"User assignment: UserId={userRole.UserId}, IsActive={userRole.IsActive}, AssignedDate={userRole.AssignedDate}");
                    }
                    
                    var userCount = userRoles.Count(ur => ur.IsActive);
                    _logger.LogInformation($"Final user count for role {role.RoleName}: {userCount}");
                    
                    roleViewModels.Add(new RoleViewModel
                    {
                        RoleId = role.RoleId,
                        RoleName = role.RoleName,
                        Description = role.Description,
                        IsSystemRole = role.IsSystemRole,
                        IsActive = role.IsActive,
                        CreatedDate = role.CreatedDate,
                        UserCount = userCount,
                        BranchAssignments = branchAssignments.Select(ba => new BranchAssignmentViewModel
                        {
                            BranchId = ba.BranchId,
                            BranchName = ba.Branch.BranchName
                        }).ToList()
                    });
                }

                var branches = await _context.Branch
                    .Where(b => b.DeletedDate == null)
                    .OrderBy(b => b.BranchName)
                    .ToListAsync();

                ViewBag.Branches = branches;
                return View(roleViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in RoleManagement Index: {ex.Message}");
                throw;
            }
        }

        // GET: /RoleManagement/Create
        public async Task<IActionResult> Create()
        {
            return View(new CreateRoleViewModel());
        }

        // POST: /RoleManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateRoleViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                // Check if role name already exists
                var existingRole = await _context.DynamicRoles
                    .FirstOrDefaultAsync(r => r.RoleName == model.RoleName && r.DeletedDate == null);

                if (existingRole != null)
                {
                    ModelState.AddModelError("RoleName", "A role with this name already exists.");
                    return View(model);
                }

                var role = await _permissionService.CreateRoleAsync(
                    model.RoleName, 
                    model.Description, 
                    currentUser.Id);

                if (role != null)
                {
                    TempData["Success"] = "Role created successfully. You can now assign it to branches.";
                    return RedirectToAction("Details", new { id = role.RoleId });
                }

                ModelState.AddModelError("", "Failed to create role.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating role: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while creating the role.");
                return View(model);
            }
        }

        // POST: /RoleManagement/AssignToBranch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignToBranch([FromBody] AssignRoleToBranchRequest request)
        {
            try
            {
                _logger.LogInformation($"=== AssignToBranch START ===");
                _logger.LogInformation($"Request data: roleId={request?.RoleId}, branchId={request?.BranchId}");
                
                if (request == null)
                {
                    _logger.LogError("Request is null");
                    return Json(new { success = false, message = "Invalid request data" });
                }
                
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                {
                    _logger.LogWarning("User not authenticated in AssignToBranch");
                    return Json(new { success = false, message = "User not authenticated" });
                }
                
                _logger.LogInformation($"Current user: {currentUser.Id}");

                // Check for existing assignment (including inactive ones due to unique constraint)
                var existingAssignment = await _context.BranchRoles
                    .FirstOrDefaultAsync(br => br.RoleId == request.RoleId && br.BranchId == request.BranchId);
                
                if (existingAssignment != null)
                {
                    _logger.LogInformation($"Found existing assignment: IsActive={existingAssignment.IsActive}");
                    
                    if (existingAssignment.IsActive)
                    {
                        _logger.LogWarning($"Role {request.RoleId} is already assigned to branch {request.BranchId}");
                        return Json(new { success = false, message = "Role is already assigned to this branch" });
                    }
                    else
                    {
                        // Reactivate the existing assignment
                        _logger.LogInformation($"Reactivating existing assignment");
                        existingAssignment.IsActive = true;
                        existingAssignment.AssignedBy = currentUser.Id;
                        existingAssignment.AssignedDate = DateTime.UtcNow;
                        
                        var updateResult = await _context.SaveChangesAsync();
                        _logger.LogInformation($"Update result: {updateResult}");
                        
                        if (updateResult > 0)
                        {
                            _logger.LogInformation($"=== SUCCESS: Role {request.RoleId} reactivated for branch {request.BranchId} ===");
                            return Json(new { success = true, message = "Role assigned to branch successfully" });
                        }
                        else
                        {
                            _logger.LogWarning("Update failed - no changes saved");
                            return Json(new { success = false, message = "Failed to update assignment" });
                        }
                    }
                }
                else
                {
                    // Create new assignment
                    _logger.LogInformation($"Creating new assignment");
                    var newBranchRole = new BranchRole
                    {
                        RoleId = request.RoleId,
                        BranchId = request.BranchId,
                        AssignedBy = currentUser.Id,
                        AssignedDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    
                    _context.BranchRoles.Add(newBranchRole);
                    var saveResult = await _context.SaveChangesAsync();
                    
                    _logger.LogInformation($"Save result: {saveResult}");
                    
                    if (saveResult > 0)
                    {
                        _logger.LogInformation($"=== SUCCESS: Role {request.RoleId} assigned to branch {request.BranchId} ===");
                        return Json(new { success = true, message = "Role assigned to branch successfully" });
                    }
                    else
                    {
                        _logger.LogWarning("Save failed - no changes saved");
                        return Json(new { success = false, message = "Failed to create assignment" });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"=== ERROR in AssignToBranch ===");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Inner exception: {ex.InnerException?.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // POST: /RoleManagement/RemoveFromBranch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromBranch([FromBody] RemoveRoleFromBranchRequest request)
        {
            try
            {
                _logger.LogInformation($"RemoveFromBranch called with roleId: {request?.RoleId}, branchId: {request?.BranchId}");
                
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                {
                    _logger.LogWarning("User not authenticated in RemoveFromBranch");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Check if user can manage the role
                if (!await _permissionService.CanManageRoleAsync(currentUser.Id, request.RoleId))
                {
                    _logger.LogWarning($"User {currentUser.Id} doesn't have permission to manage role {request.RoleId}");
                    return Json(new { success = false, message = "You don't have permission to manage this role" });
                }

                var success = await _permissionService.RemoveRoleFromBranchAsync(request.RoleId, request.BranchId);
                
                if (success)
                {
                    _logger.LogInformation($"Role {request.RoleId} successfully removed from branch {request.BranchId} by user {currentUser.Id}");
                    return Json(new { success = true, message = "Role removed from branch successfully" });
                }
                else
                {
                    _logger.LogWarning($"Failed to remove role {request.RoleId} from branch {request.BranchId} by user {currentUser.Id}");
                    return Json(new { success = false, message = "Failed to remove role from branch" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error removing role from branch: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while removing the role: " + ex.Message });
            }
        }

        // GET: /RoleManagement/GetUnassignedBranches
        [HttpGet]
        public async Task<IActionResult> GetUnassignedBranches(int roleId)
        {
            try
            {
                _logger.LogInformation($"Getting unassigned branches for role {roleId}");
                
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                {
                    _logger.LogWarning("User not authenticated");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Get branches where this role is NOT assigned
                var assignedBranchIds = await _context.BranchRoles
                    .Where(br => br.RoleId == roleId && br.IsActive)
                    .Select(br => br.BranchId)
                    .ToListAsync();
                _logger.LogInformation($"Found {assignedBranchIds.Count} branches where role {roleId} is already assigned");

                var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
                _logger.LogInformation($"User {currentUser.Id} has access to {accessibleBranchIds.Count} branches");

                var unassignedBranches = await _context.Branch
                    .Where(b => b.DeletedDate == null && 
                               !assignedBranchIds.Contains(b.BranchId) &&
                               accessibleBranchIds.Contains(b.BranchId))
                    .Select(b => new { id = b.BranchId, name = b.BranchName })
                    .ToListAsync();
                _logger.LogInformation($"Found {unassignedBranches.Count} unassigned branches for role {roleId}");

                return Json(new { success = true, branches = unassignedBranches });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting unassigned branches: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while fetching branches" });
            }
        }

        // GET: /RoleManagement/GetAssignedBranches
        [HttpGet]
        public async Task<IActionResult> GetAssignedBranches(int roleId)
        {
            try
            {
                _logger.LogInformation($"Getting assigned branches for role {roleId}");
                
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                {
                    _logger.LogWarning("User not authenticated");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Get branches where this role IS assigned
                var assignedBranches = await _context.BranchRoles
                    .Where(br => br.RoleId == roleId && br.IsActive)
                    .Include(br => br.Branch)
                    .Where(br => br.Branch.DeletedDate == null)
                    .Select(br => new { id = br.BranchId, name = br.Branch.BranchName })
                    .ToListAsync();
                _logger.LogInformation($"Found {assignedBranches.Count} branches assigned to role {roleId}");

                return Json(new { success = true, branches = assignedBranches });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting assigned branches: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while fetching assigned branches" });
            }
        }

        // GET: /RoleManagement/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                if (!await _permissionService.CanManageRoleAsync(currentUser.Id, id))
                {
                    return RedirectToAction("AccessDenied", "Account");
                }

                var role = await _context.DynamicRoles
                    .FirstOrDefaultAsync(r => r.RoleId == id);

                if (role == null || role.DeletedDate != null)
                {
                    return NotFound();
                }

                var permissions = await _permissionService.GetRolePermissionsAsync(id);
                var allPermissions = await _permissionService.GetAllPermissionsAsync();

                // Get assigned users for this role
                var assignedUsers = await _context.DynamicUserRoles
                    .Where(ur => ur.RoleId == id && ur.IsActive)
                    .Include(ur => ur.User)
                    .ThenInclude(u => u.Employee)
                    .Select(ur => new UserRoleViewModel
                    {
                        UserId = ur.UserId,
                        FullName = $"{ur.User.Employee.FirstName} {ur.User.Employee.LastName}",
                        Email = ur.User.Email,
                        AssignedDate = ur.AssignedDate
                    })
                    .ToListAsync();

                _logger.LogInformation($"Role {id} has {assignedUsers.Count} assigned users");
                foreach (var user in assignedUsers)
                {
                    _logger.LogInformation($"Assigned user: {user.FullName} ({user.Email}) - Assigned on: {user.AssignedDate}");
                }

                // Get branch assignments
                var branchAssignments = await _context.BranchRoles
                    .Where(br => br.RoleId == id && br.IsActive)
                    .Include(br => br.Branch)
                    .ToListAsync();

                var viewModel = new RoleDetailsViewModel
                {
                    Role = role,
                    Permissions = permissions,
                    AllPermissions = allPermissions,
                    Users = assignedUsers,
                    BranchAssignments = branchAssignments.Select(ba => new BranchAssignmentViewModel
                    {
                        BranchId = ba.BranchId,
                        BranchName = ba.Branch.BranchName,
                        AssignedDate = ba.AssignedDate
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting role details: {ex.Message}");
                throw;
            }
        }

        // POST: /RoleManagement/AssignPermission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPermission([FromBody] AssignPermissionRequest request)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                    return Json(new { success = false, message = "User not authenticated" });

                if (!await _permissionService.CanManageRoleAsync(currentUser.Id, request.RoleId))
                {
                    return Json(new { success = false, message = "You don't have permission to manage this role" });
                }

                var success = await _permissionService.AssignPermissionToRoleAsync(request.RoleId, request.PermissionId, currentUser.Id);
                
                if (success)
                {
                    return Json(new { success = true, message = "Permission assigned successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to assign permission" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning permission: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while assigning the permission" });
            }
        }

        // POST: /RoleManagement/RemovePermission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePermission([FromBody] RemovePermissionRequest request)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                    return Json(new { success = false, message = "User not authenticated" });

                if (!await _permissionService.CanManageRoleAsync(currentUser.Id, request.RoleId))
                {
                    return Json(new { success = false, message = "You don't have permission to manage this role" });
                }

                var success = await _permissionService.RemovePermissionFromRoleAsync(request.RoleId, request.PermissionId);
                
                if (success)
                {
                    return Json(new { success = true, message = "Permission removed successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to remove permission" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error removing permission: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while removing the permission" });
            }
        }

        // GET: /RoleManagement/AssignUsers/5
        public async Task<IActionResult> AssignUsers(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                if (!await _permissionService.CanManageRoleAsync(currentUser.Id, id))
                {
                    return RedirectToAction("AccessDenied", "Account");
                }

                var role = await _context.DynamicRoles
                    .FirstOrDefaultAsync(r => r.RoleId == id);

                if (role == null || role.DeletedDate != null)
                {
                    return NotFound();
                }

                // Get accessible branch IDs
                var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);

                // Get assigned users
                var assignedUserIds = await _context.DynamicUserRoles
                    .Where(ur => ur.RoleId == id && ur.IsActive)
                    .Select(ur => ur.UserId)
                    .ToListAsync();

                var assignedUsers = await _userManager.Users
                    .Include(u => u.Employee)
                    .ThenInclude(e => e.Branch)
                    .Where(u => assignedUserIds.Contains(u.Id) && 
                               u.Employee != null && 
                               accessibleBranchIds.Contains(u.Employee.BranchId))
                    .Select(u => new
                    {
                        UserId = u.Id,
                        FullName = $"{u.Employee.FirstName} {u.Employee.LastName}",
                        Email = u.Email,
                        BranchName = u.Employee.Branch.BranchName
                    })
                    .ToListAsync();

                // Get available users (users in accessible branches who don't have this role)
                var availableUsers = await _userManager.Users
                    .Include(u => u.Employee)
                    .ThenInclude(e => e.Branch)
                    .Where(u => !assignedUserIds.Contains(u.Id) && 
                               u.Employee != null && 
                               accessibleBranchIds.Contains(u.Employee.BranchId))
                    .Select(u => new
                    {
                        UserId = u.Id,
                        FullName = $"{u.Employee.FirstName} {u.Employee.LastName}",
                        Email = u.Email,
                        BranchName = u.Employee.Branch.BranchName
                    })
                    .ToListAsync();

                var viewModel = new AssignUsersViewModel
                {
                    Role = role,
                    AvailableUsers = availableUsers.Cast<dynamic>().ToList(),
                    AssignedUsers = assignedUsers.Cast<dynamic>().ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting assign users view: {ex.Message}");
                throw;
            }
        }

        // POST: /RoleManagement/AssignUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignUser([FromBody] AssignUserRequest request)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                    return Json(new { success = false, message = "User not authenticated" });

                if (!await _permissionService.CanManageRoleAsync(currentUser.Id, request.RoleId))
                {
                    return Json(new { success = false, message = "You don't have permission to manage this role" });
                }

                var success = await _permissionService.AssignRoleToUserAsync(request.UserId, request.RoleId, currentUser.Id);
                
                if (success)
                {
                    return Json(new { success = true, message = "User assigned to role successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to assign user to role" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error assigning user to role: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while assigning the user" });
            }
        }

        // POST: /RoleManagement/RemoveUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUser([FromBody] AssignUserRequest request)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                    return Json(new { success = false, message = "User not authenticated" });

                if (!await _permissionService.CanManageRoleAsync(currentUser.Id, request.RoleId))
                {
                    return Json(new { success = false, message = "You don't have permission to manage this role" });
                }

                var success = await _permissionService.RemoveRoleFromUserAsync(request.UserId, request.RoleId);
                
                if (success)
                {
                    return Json(new { success = true, message = "User removed from role successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to remove user from role" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error removing user from role: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while removing the user" });
            }
        }

        // POST: /RoleManagement/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            try
            {
                _logger.LogInformation($"Delete called with roleId: {id}");
                
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                {
                    _logger.LogWarning("User not authenticated in Delete");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                if (!await _permissionService.CanManageRoleAsync(currentUser.Id, id))
                {
                    _logger.LogWarning($"User {currentUser.Id} doesn't have permission to manage role {id}");
                    return Json(new { success = false, message = "You don't have permission to manage this role" });
                }

                var role = await _context.DynamicRoles.FindAsync(id);
                if (role == null || role.DeletedDate != null)
                {
                    _logger.LogWarning($"Role {id} not found");
                    return Json(new { success = false, message = "Role not found" });
                }

                if (role.IsSystemRole)
                {
                    _logger.LogWarning($"Attempted to delete system role {id}");
                    return Json(new { success = false, message = "Cannot delete system roles" });
                }

                var success = await _permissionService.DeleteRoleAsync(id);
                
                if (success)
                {
                    _logger.LogInformation($"Role {id} ({role.RoleName}) successfully deleted by user {currentUser.Id}");
                    return Json(new { success = true, message = "Role deleted successfully" });
                }
                else
                {
                    _logger.LogWarning($"Failed to delete role {id} by user {currentUser.Id}");
                    return Json(new { success = false, message = "Failed to delete role. System roles cannot be deleted." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting role: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while deleting the role: " + ex.Message });
            }
        }
    }

    // View Models
    public class RoleViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string? Description { get; set; }
        public bool IsSystemRole { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public int UserCount { get; set; }
        public List<BranchAssignmentViewModel> BranchAssignments { get; set; } = new List<BranchAssignmentViewModel>();
    }

    public class BranchAssignmentViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public DateTime AssignedDate { get; set; }
    }

    public class CreateRoleViewModel
    {
        [Required]
        [StringLength(100)]
        public string RoleName { get; set; }
        
        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class RoleDetailsViewModel
    {
        public Role Role { get; set; }
        public List<Permission> Permissions { get; set; }
        public List<Permission> AllPermissions { get; set; }
        public List<UserRoleViewModel> Users { get; set; }
        public List<BranchAssignmentViewModel> BranchAssignments { get; set; } = new List<BranchAssignmentViewModel>();
    }

    public class UserRoleViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime AssignedDate { get; set; }
    }

    public class AssignUsersViewModel
    {
        public Role Role { get; set; }
        public List<dynamic> AvailableUsers { get; set; }
        public List<dynamic> AssignedUsers { get; set; }
    }

    // Request Models
    public class AssignPermissionRequest
    {
        public int RoleId { get; set; }
        public int PermissionId { get; set; }
    }

    public class RemovePermissionRequest
    {
        public int RoleId { get; set; }
        public int PermissionId { get; set; }
    }

    public class AssignUserRequest
    {
        public string UserId { get; set; }
        public int RoleId { get; set; }
    }

    public class AssignRoleToBranchRequest
    {
        public int RoleId { get; set; }
        public int BranchId { get; set; }
    }

    public class RemoveRoleFromBranchRequest
    {
        public int RoleId { get; set; }
        public int BranchId { get; set; }
    }
} 