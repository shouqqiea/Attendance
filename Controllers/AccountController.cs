using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using LetsCheckIn.Helpers;
using Microsoft.Extensions.Logging;

public class CreateUserViewModel
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool Status { get; set; }
    public int BranchId { get; set; }
    public string? Password { get; set; }
}

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IBranchAccessService _branchAccessService;
    private readonly LetsCheckIn.Helpers.IDynamicPermissionService _permissionService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager, 
        UserManager<ApplicationUser> userManager, 
        ApplicationDbContext db, 
        IBranchAccessService branchAccessService,
        LetsCheckIn.Helpers.IDynamicPermissionService permissionService,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _branchAccessService = branchAccessService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public IActionResult Login()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Dashboard", "LeaveManagement");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        // First sign out any existing session
        await _signInManager.SignOutAsync();

        var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // Get the logged-in user
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                // Check if employee record exists
                var employeeExists = await _db.Employee.AnyAsync(e => e.UserId == user.Id);
                if (!employeeExists)
                {
                    // Create default employee record
                    var employee = new Employee
                    {
                        UserId = user.Id,
                        FirstName = "Default",
                        LastName = "User",
                        Email = email,
                        BranchId = _db.Branch.First().BranchId // Get first available branch
                    };
                    _db.Employee.Add(employee);
                    await _db.SaveChangesAsync();
                }

                // Update user's BranchId based on employee record
                var emp = await _db.Employee.FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (emp != null && user.BranchId != emp.BranchId)
                {
                    user.BranchId = emp.BranchId;
                    await _userManager.UpdateAsync(user);
                }
            }

            return RedirectToAction("Dashboard", "LeaveManagement");
        }

        ModelState.AddModelError("", "Invalid login attempt.");
        return View();
    }

    public async Task<IActionResult> Logout()
    {
        // Clear the existing external cookie
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        // Clear the main authentication cookie
        await _signInManager.SignOutAsync();
        // Clear all cookies
        foreach (var cookie in Request.Cookies.Keys)
        {
            Response.Cookies.Delete(cookie);
        }
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() => View();

    // GET: /Account/CreateBranch
    public async Task<IActionResult> CreateBranch(int? parentBranchId = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Check if user can create branches
        if (!await _branchAccessService.CanCreateBranchAsync(currentUser.Id))
        {
            return RedirectToAction("AccessDenied");
        }

        // If creating a child branch, validate parent branch access
        if (parentBranchId.HasValue)
        {
            if (!await _branchAccessService.CanCreateChildBranchAsync(currentUser.Id, parentBranchId.Value))
            {
                return RedirectToAction("AccessDenied");
            }
        }

        var model = new CreateBranchViewModel 
        { 
            EnableLocation = true,
            ParentBranchId = parentBranchId
        };

        // Populate available parent branches for Corporate users
        await PopulateAvailableParentBranches(model, currentUser.Id);

        return View(model);
    }

    // POST: /Account/CreateBranch
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBranch(CreateBranchViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Check if user can create branches
        if (!await _branchAccessService.CanCreateBranchAsync(currentUser.Id))
        {
            return RedirectToAction("AccessDenied");
        }

        // Validate hierarchy rules
        if (model.ParentBranchId.HasValue)
        {
            if (!await _branchAccessService.CanCreateChildBranchAsync(currentUser.Id, model.ParentBranchId.Value))
            {
                ModelState.AddModelError("ParentBranchId", "You don't have permission to create child branches under the selected parent.");
            }

            if (!await _branchAccessService.IsValidBranchHierarchyAsync(model.ParentBranchId.Value, model.BranchType))
            {
                ModelState.AddModelError("BranchType", "The selected parent branch cannot have children of this type.");
            }
        }
        else if (!await _branchAccessService.IsSuperAdminAsync(currentUser.Id))
        {
            // Only SuperAdmin can create root branches
            ModelState.AddModelError("ParentBranchId", "Only SuperAdmin can create root branches. Please select a parent branch.");
        }

        if (!ModelState.IsValid || 
            string.IsNullOrWhiteSpace(model.AdminName) || 
            string.IsNullOrWhiteSpace(model.AdminEmail) || 
            string.IsNullOrWhiteSpace(model.BranchName) || 
            !model.EnableLocation)
        {
            if (!model.EnableLocation)
                ModelState.AddModelError("EnableLocation", "At least Location must be enabled.");
            
            // Repopulate parent branches on validation failure
            await PopulateAvailableParentBranches(model, currentUser.Id);
            return View(model);
        }

        // Create Admin
        var admin = new Admin { Name = model.AdminName, Email = model.AdminEmail };
        _db.Admin.Add(admin);
        await _db.SaveChangesAsync();

        // Check if this is the first branch being created
        var isFirstBranch = !await _db.Branch.AnyAsync();

        // Create Branch with hierarchy support
        var branch = new Branch
        {
            BranchName = model.BranchName,
            AdminId = admin.AdminId,
            BranchType = model.BranchType,
            ParentBranchId = model.ParentBranchId,
            CreatedDate = DateTime.UtcNow,
            // Set as superadmin branch only if it's the first branch and created by a superadmin
            IsSuperAdminBranch = isFirstBranch && await _branchAccessService.IsSuperAdminAsync(currentUser.Id)
        };
        _db.Branch.Add(branch);
        await _db.SaveChangesAsync();

        // Create BranchSetup
        var setup = new BranchSetup
        {
            BranchId = branch.BranchId,
            EnableLocation = model.EnableLocation,
            EnableNetwork = model.EnableNetwork,
            EnableBiometric = model.EnableBiometric,
            CreatedDate = DateTime.UtcNow
        };
        _db.BranchSetup.Add(setup);
        await _db.SaveChangesAsync();

        // If this is the first branch and created by a superadmin, assign the SuperAdmin role
        if (branch.IsSuperAdminBranch)
        {
            var superAdminRole = await _db.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == "SuperAdmin");
            if (superAdminRole != null)
            {
                var branchRole = new BranchRole
                {
                    BranchId = branch.BranchId,
                    RoleId = superAdminRole.RoleId,
                    AssignedBy = currentUser.Id,
                    IsActive = true
                };
                _db.BranchRoles.Add(branchRole);
                await _db.SaveChangesAsync();
            }
        }

        // Prepare details for success view
        var details = new BranchCreationSuccessViewModel
        {
            BranchName = branch.BranchName,
            AdminName = admin.Name,
            AdminEmail = admin.Email,
            EnableLocation = setup.EnableLocation,
            EnableNetwork = setup.EnableNetwork,
            EnableBiometric = setup.EnableBiometric
        };

        return View("BranchCreationSuccess", details);
    }

    // GET: /Account/CreateUser
    public async Task<IActionResult> CreateUser()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
        var branches = await _db.Branch
            .Where(b => accessibleBranchIds.Contains(b.BranchId))
            .ToListAsync();

        ViewBag.Branches = branches;
        
        // ✅ Roles will be loaded dynamically based on selected branch
        // No need to pre-populate ViewBag.Roles as it causes confusion with unassigned roles
        ViewBag.Roles = new List<string>(); // Empty list for backward compatibility
        
        return View();
    }

    // POST: /Account/CreateUser
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (!ModelState.IsValid || 
            string.IsNullOrWhiteSpace(model.FirstName) || 
            string.IsNullOrWhiteSpace(model.LastName) || 
            string.IsNullOrWhiteSpace(model.Email) || 
            string.IsNullOrWhiteSpace(model.Role) || 
            string.IsNullOrWhiteSpace(model.Password))
        {
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
            ViewBag.Branches = await _db.Branch
                .Where(b => accessibleBranchIds.Contains(b.BranchId))
                .ToListAsync();
            
            // ✅ Roles will be loaded dynamically via JavaScript based on selected branch
            ViewBag.Roles = new List<string>(); // Empty list for backward compatibility
            
            return View(model);
        }

        // Check permissions
        if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, model.BranchId))
        {
            ModelState.AddModelError("", "You don't have permission to create users in this branch.");
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
            ViewBag.Branches = await _db.Branch
                .Where(b => accessibleBranchIds.Contains(b.BranchId))
                .ToListAsync();
            
            // ✅ Roles will be loaded dynamically via JavaScript based on selected branch
            ViewBag.Roles = new List<string>(); // Empty list for backward compatibility
            
            return View(model);
        }

        // Validate that the role is assigned to the branch
        var targetBranchRoles = await _permissionService.GetRolesForBranchAsync(model.BranchId);
        if (!targetBranchRoles.Any(r => r.RoleName == model.Role))
        {
            ModelState.AddModelError("", "The selected role is not available for this branch.");
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
            ViewBag.Branches = await _db.Branch
                .Where(b => accessibleBranchIds.Contains(b.BranchId))
                .ToListAsync();
            
            // ✅ Roles will be loaded dynamically via JavaScript based on selected branch
            ViewBag.Roles = new List<string>(); // Empty list for backward compatibility
            
            return View(model);
        }

        if (!await _branchAccessService.CanAssignRoleAsync(currentUser.Id, model.Role, model.BranchId))
        {
            ModelState.AddModelError("", "You don't have permission to assign this role.");
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
            ViewBag.Branches = await _db.Branch
                .Where(b => accessibleBranchIds.Contains(b.BranchId))
                .ToListAsync();
            
            // ✅ Roles will be loaded dynamically via JavaScript based on selected branch
            ViewBag.Roles = new List<string>(); // Empty list for backward compatibility
            
            return View(model);
        }

        // Create Identity User
        var user = new ApplicationUser { 
            UserName = model.Email, 
            Email = model.Email,
            BranchId = model.BranchId
        };
        var result = await _userManager.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
            ViewBag.Branches = await _db.Branch
                .Where(b => accessibleBranchIds.Contains(b.BranchId))
                .ToListAsync();
            
            // ✅ Roles will be loaded dynamically via JavaScript based on selected branch
            ViewBag.Roles = new List<string>(); // Empty list for backward compatibility
            
            return View(model);
        }

        // Create Employee
        var employee = new Employee
        {
            UserId = user.Id,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email ?? string.Empty,
            BranchId = model.BranchId
        };
        _db.Employee.Add(employee);
        await _db.SaveChangesAsync();

        // Assign to ASP.NET Identity role (which was missing before!)
        await _userManager.AddToRoleAsync(user, model.Role);

        // Assign to Dynamic Role System
        var dynamicRole = await _db.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == model.Role);
        if (dynamicRole != null)
        {
            await _permissionService.AssignRoleToUserAsync(user.Id, dynamicRole.RoleId, currentUser.Id);
        }

        // ✅ AUTO-INHERIT ROLES FROM PARENT BRANCH
        // Check if this is a child branch and automatically inherit roles from parent
        var childBranch = await _db.Branch
            .Include(b => b.ParentBranch)
            .FirstOrDefaultAsync(b => b.BranchId == model.BranchId);

        if (childBranch?.ParentBranchId != null)
        {
            _logger.LogInformation($"User created in child branch {childBranch.BranchName} (ID: {childBranch.BranchId}). " +
                                 $"Inheriting roles from parent branch {childBranch.ParentBranch?.BranchName} (ID: {childBranch.ParentBranchId})");

            // Inherit roles from parent branch (this will exclude parent-only roles)
            var inheritanceSuccess = await _permissionService.InheritRolesFromParentAsync(
                user.Id, 
                childBranch.ParentBranchId.Value, 
                currentUser.Id);

            if (inheritanceSuccess)
            {
                _logger.LogInformation($"Successfully inherited roles from parent branch for user {user.Email}");
            }
            else
            {
                _logger.LogWarning($"Failed to inherit roles from parent branch for user {user.Email}");
            }
        }

        TempData["Success"] = "User created successfully. " + 
                             (childBranch?.ParentBranchId != null ? "Roles have been automatically inherited from the parent branch." : "");
        return RedirectToAction("CreateUser");
    }

    // GET: /Account/BranchList
    public async Task<IActionResult> BranchList(string? search)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        _logger.LogInformation($"BranchList - User: {currentUser.Email}, UserId: {currentUser.Id}");

        // Check if user is SuperAdmin for debugging
        var isSuperAdmin = await _branchAccessService.IsSuperAdminAsync(currentUser.Id);
        _logger.LogInformation($"BranchList - IsSuperAdmin: {isSuperAdmin}");

        // Check accessible branch IDs for debugging  
        var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
        _logger.LogInformation($"BranchList - Accessible Branch IDs: [{string.Join(", ", accessibleBranchIds)}]");

        // Check total branches in database for debugging
        var totalBranches = await _db.Branch.Where(b => b.DeletedDate == null).CountAsync();
        _logger.LogInformation($"BranchList - Total branches in database: {totalBranches}");

        // Use the existing hierarchy method to get properly structured data
        var hierarchyViewModel = await GetBranchHierarchyAsync(currentUser.Id);
        _logger.LogInformation($"BranchList - Retrieved {hierarchyViewModel.AccessibleBranches.Count} accessible branches");

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            var originalCount = hierarchyViewModel.AccessibleBranches.Count;
            hierarchyViewModel.AccessibleBranches = hierarchyViewModel.AccessibleBranches.Where(b => 
                b.BranchName.Contains(search, StringComparison.OrdinalIgnoreCase) || 
                (b.ParentBranchName != null && b.ParentBranchName.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
            _logger.LogInformation($"BranchList - After search filter '{search}': {hierarchyViewModel.AccessibleBranches.Count} branches (was {originalCount})");
        }

        // Build hierarchical tree structure for display
        hierarchyViewModel.BranchTree = BuildHierarchicalTree(hierarchyViewModel.AccessibleBranches);
        _logger.LogInformation($"BranchList - Built hierarchy tree with {hierarchyViewModel.BranchTree.Count} root branches");

        ViewBag.Search = search;
        ViewBag.CanCreateBranch = await _branchAccessService.CanCreateBranchAsync(currentUser.Id);
        ViewBag.IsSuperAdmin = await _branchAccessService.IsSuperAdminAsync(currentUser.Id);
        
        return View(hierarchyViewModel);
    }

    /// <summary>
    /// Builds a hierarchical tree structure from a flat list of branch items
    /// </summary>
    private List<BranchHierarchyItem> BuildHierarchicalTree(List<BranchHierarchyItem> flatList)
    {
        // Create a dictionary for fast lookup
        var branchDict = flatList.ToDictionary(b => b.BranchId);
        var rootBranches = new List<BranchHierarchyItem>();

        foreach (var branch in flatList)
        {
            if (branch.ParentBranchId.HasValue && branchDict.ContainsKey(branch.ParentBranchId.Value))
            {
                // This is a child branch - add it to parent's children
                var parent = branchDict[branch.ParentBranchId.Value];
                parent.Children.Add(branch);
            }
            else
            {
                // This is a root branch
                rootBranches.Add(branch);
            }
        }

        return rootBranches.OrderBy(b => b.BranchName).ToList();
    }

    // POST: /Account/DeleteBranch
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBranch([FromBody] int id)
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var branch = await _db.Branch
                .Include(b => b.BranchSetup)
                .FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
            {
                return Json(new { success = false, message = $"Branch with ID {id} not found." });
            }

            // Check if this is the only remaining branch
            var activeBranchCount = await _db.Branch.CountAsync(b => b.DeletedDate == null && b.BranchId != id);
            if (activeBranchCount == 0)
            {
                return Json(new { success = false, message = "Cannot delete the last remaining branch. At least one branch must exist." });
            }

            // Get the main/first remaining active branch to transfer users to
            var transferToBranch = await _db.Branch
                .Where(b => b.DeletedDate == null && b.BranchId != id)
                .OrderBy(b => b.BranchId)
                .FirstOrDefaultAsync();

            if (transferToBranch == null)
            {
                return Json(new { success = false, message = "No active branch found to transfer users to." });
            }

            // Soft delete the branch and its setup
            branch.DeletedDate = DateTime.UtcNow;
            if (branch.BranchSetup != null)
            {
                branch.BranchSetup.DeletedDate = DateTime.UtcNow;
            }

            // Deactivate all role assignments for this branch to prevent them from showing in role management
            var branchRoles = await _db.BranchRoles
                .Where(br => br.BranchId == id && br.IsActive)
                .ToListAsync();

            foreach (var branchRole in branchRoles)
            {
                branchRole.IsActive = false;
            }

            // Handle employees and their associated users
            var employees = await _db.Employee
                .Include(e => e.User)
                .Where(e => e.BranchId == id && e.DeletedDate == null)
                .ToListAsync();

            foreach (var employee in employees)
            {
                // Soft delete the employee record
                employee.DeletedDate = DateTime.UtcNow;
                employee.Status = false; // Deactivate employee

                // Transfer the user to the main branch to prevent login issues
                if (employee.User != null)
                {
                    employee.User.BranchId = transferToBranch.BranchId;
                    
                    // Also deactivate all their dynamic user roles to prevent access issues
                    var userRoles = await _db.DynamicUserRoles
                        .Where(ur => ur.UserId == employee.UserId && ur.IsActive)
                        .ToListAsync();
                    
                    foreach (var userRole in userRoles)
                    {
                        userRole.IsActive = false;
                    }
                }
            }

            // Get the "Pending" and "Cancelled" status IDs
            var pendingStatus = await _db.StatusTypes.FirstOrDefaultAsync(st => st.StatusName == "Pending");
            var cancelledStatus = await _db.StatusTypes.FirstOrDefaultAsync(st => st.StatusName == "Cancelled");
            
            if (pendingStatus != null && cancelledStatus != null)
            {
                // Cancel any pending leave requests for this branch
                var leaveRequests = await _db.LeaveRequests
                    .Where(lr => employees.Select(e => e.EmployeeId).Contains(lr.EmployeeId) && lr.StatusId == pendingStatus.StatusId)
                    .ToListAsync();

                foreach (var leaveRequest in leaveRequests)
                {
                    leaveRequest.StatusId = cancelledStatus.StatusId;
                    leaveRequest.RejectedReason = "Branch deleted - request automatically cancelled";
                    leaveRequest.ActionPerformedBy = currentUser.Id;
                    leaveRequest.ActionPerformedOn = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
            
            var message = $"Branch deleted successfully. {employees.Count} users have been transferred to '{transferToBranch.BranchName}' but their roles have been deactivated for security. Please reassign roles as needed.";
            return Json(new { success = true, message = message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while deleting the branch.", error = ex.Message });
        }
    }

    // GET: /Account/GetBranchDetails
    [HttpGet]
    public async Task<IActionResult> GetBranchDetails(int id)
    {
        try
        {
            var branch = await _db.Branch
                .Include(b => b.Admin)
                .Include(b => b.BranchSetup)
                .FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
            {
                return Json(new { success = false, message = $"Branch with ID {id} not found." });
            }

            var viewModel = new UpdateBranchViewModel
            {
                BranchId = branch.BranchId,
                BranchName = branch.BranchName,
                AdminName = branch.Admin?.Name ?? string.Empty,
                AdminEmail = branch.Admin?.Email ?? string.Empty,
                EnableLocation = branch.BranchSetup?.EnableLocation ?? false,
                EnableNetwork = branch.BranchSetup?.EnableNetwork ?? false,
                EnableBiometric = branch.BranchSetup?.EnableBiometric ?? false
            };

            return Json(new { success = true, data = viewModel });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while fetching branch details.", error = ex.Message });
        }
    }

    // POST: /Account/UpdateBranch
    [HttpPost]
    // [ValidateAntiForgeryToken] // Temporarily disabled for testing
    public async Task<IActionResult> UpdateBranch([FromBody] UpdateBranchViewModel model)
    {
        try
        {
            _logger.LogInformation($"UpdateBranch called with BranchId: {model.BranchId}, BranchName: {model.BranchName}, EnableLocation: {model.EnableLocation}, EnableNetwork: {model.EnableNetwork}, EnableBiometric: {model.EnableBiometric}");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning($"ModelState is invalid. Errors: {string.Join(", ", errors)}");
                return Json(new { success = false, message = "Please check the form for errors.", errors = errors });
            }

            var branch = await _db.Branch
                .Include(b => b.Admin)
                .Include(b => b.BranchSetup)
                .FirstOrDefaultAsync(b => b.BranchId == model.BranchId);

            if (branch == null)
            {
                _logger.LogWarning($"Branch with ID {model.BranchId} not found");
                return Json(new { success = false, message = $"Branch with ID {model.BranchId} not found." });
            }

            _logger.LogInformation($"Found branch: {branch.BranchName}, Admin: {branch.Admin?.Name}, Setup exists: {branch.BranchSetup != null}");

            // Update branch name
            var oldBranchName = branch.BranchName;
            branch.BranchName = model.BranchName;
            _logger.LogInformation($"Updated branch name from '{oldBranchName}' to '{branch.BranchName}'");

            // Update admin details
            if (branch.Admin != null)
            {
                var oldAdminName = branch.Admin.Name;
                var oldAdminEmail = branch.Admin.Email;
                branch.Admin.Name = model.AdminName;
                branch.Admin.Email = model.AdminEmail;
                _logger.LogInformation($"Updated admin from '{oldAdminName}' ({oldAdminEmail}) to '{branch.Admin.Name}' ({branch.Admin.Email})");
            }
            else
            {
                _logger.LogWarning("Branch.Admin is null, cannot update admin details");
            }

            // Update branch setup
            if (branch.BranchSetup != null)
            {
                var oldLocation = branch.BranchSetup.EnableLocation;
                var oldNetwork = branch.BranchSetup.EnableNetwork;
                var oldBiometric = branch.BranchSetup.EnableBiometric;
                
                branch.BranchSetup.EnableLocation = model.EnableLocation;
                branch.BranchSetup.EnableNetwork = model.EnableNetwork;
                branch.BranchSetup.EnableBiometric = model.EnableBiometric;
                
                _logger.LogInformation($"Updated branch setup - Location: {oldLocation} -> {branch.BranchSetup.EnableLocation}, Network: {oldNetwork} -> {branch.BranchSetup.EnableNetwork}, Biometric: {oldBiometric} -> {branch.BranchSetup.EnableBiometric}");
            }
            else
            {
                _logger.LogWarning("Branch.BranchSetup is null, creating new setup");
                branch.BranchSetup = new BranchSetup
                {
                    BranchId = branch.BranchId,
                    EnableLocation = model.EnableLocation,
                    EnableNetwork = model.EnableNetwork,
                    EnableBiometric = model.EnableBiometric,
                    CreatedDate = DateTime.UtcNow
                };
                _db.BranchSetup.Add(branch.BranchSetup);
                _logger.LogInformation($"Created new branch setup with Location: {model.EnableLocation}, Network: {model.EnableNetwork}, Biometric: {model.EnableBiometric}");
            }

            var changes = await _db.SaveChangesAsync();
            _logger.LogInformation($"SaveChangesAsync completed. {changes} entities were saved.");
            
            return Json(new { success = true, message = "Branch updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in UpdateBranch: {ex.Message}\nStack trace: {ex.StackTrace}");
            return Json(new { success = false, message = "An error occurred while updating the branch.", error = ex.Message });
        }
    }

    // GET: /Account/ManageBranch/{id}
    public async Task<IActionResult> ManageBranch(int id)
    {
        var branch = await _db.Branch
            .FirstOrDefaultAsync(b => b.BranchId == id && b.DeletedDate == null);

        if (branch == null)
        {
            return NotFound("Branch not found.");
        }

        return View(branch);
    }

    // GET: /Account/GetLeaveTypes
    [HttpGet]
    public async Task<IActionResult> GetLeaveTypes(int branchId)
    {
        try
        {
            var leaveTypes = await _db.LeaveTypes
                .Where(lt => lt.BranchId == branchId && lt.DeletedDate == null)
                .Select(lt => new LeaveTypeViewModel
                {
                    LeaveTypeId = lt.LeaveTypeId,
                    Name = lt.Name,
                    Description = lt.Description,
                    DefaultDays = lt.DefaultDays,
                    IsActive = lt.IsActive,
                    BranchId = lt.BranchId
                })
                .ToListAsync();

            return Json(new { success = true, data = leaveTypes });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while fetching leave types.", error = ex.Message });
        }
    }

    // POST: /Account/SaveLeaveType
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLeaveType([FromBody] LeaveTypeViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Please check the form for errors.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            if (model.LeaveTypeId.HasValue)
            {
                // Update existing leave type
                var leaveType = await _db.LeaveTypes.FindAsync(model.LeaveTypeId.Value);
                if (leaveType == null)
                {
                    return Json(new { success = false, message = "Leave type not found." });
                }

                leaveType.Name = model.Name;
                leaveType.Description = model.Description;
                leaveType.DefaultDays = model.DefaultDays ?? 0;
                leaveType.IsActive = model.IsActive;
            }
            else
            {
                // Create new leave type
                var leaveType = new LeaveType
                {
                    Name = model.Name,
                    Description = model.Description,
                    DefaultDays = model.DefaultDays ?? 0,
                    IsActive = model.IsActive,
                    BranchId = model.BranchId,
                    CreatedDate = DateTime.UtcNow
                };
                _db.LeaveTypes.Add(leaveType);
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = model.LeaveTypeId.HasValue ? "Leave type updated successfully." : "Leave type created successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while saving the leave type.", error = ex.Message });
        }
    }

    // POST: /Account/DeleteLeaveType
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLeaveType([FromBody] int id)
    {
        try
        {
            var leaveType = await _db.LeaveTypes.FindAsync(id);
            if (leaveType == null)
            {
                return Json(new { success = false, message = "Leave type not found." });
            }

            leaveType.DeletedDate = DateTime.UtcNow;
            leaveType.IsActive = false;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Leave type deleted successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while deleting the leave type.", error = ex.Message });
        }
    }

    // GET: /Account/UpdateBranch/{id}
    public async Task<IActionResult> UpdateBranch(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Check if user can access this branch
        if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, id))
        {
            return RedirectToAction("AccessDenied");
        }

        var branch = await _db.Branch
            .Include(b => b.Admin)
            .Include(b => b.BranchSetup)
            .Include(b => b.ParentBranch)
            .Include(b => b.ChildBranches.Where(c => c.DeletedDate == null))
                .ThenInclude(c => c.Employees.Where(e => e.DeletedDate == null))
            .FirstOrDefaultAsync(b => b.BranchId == id && b.DeletedDate == null);

        if (branch == null)
        {
            return NotFound("Branch not found.");
        }

        var viewModel = new UpdateBranchViewModel
        {
            BranchId = branch.BranchId,
            BranchName = branch.BranchName,
            BranchType = branch.BranchType,
            ParentBranchId = branch.ParentBranchId,
            ParentBranchName = branch.ParentBranch?.BranchName,
            AdminName = branch.Admin?.Name ?? string.Empty,
            AdminEmail = branch.Admin?.Email ?? string.Empty,
            EnableLocation = branch.BranchSetup?.EnableLocation ?? false,
            EnableNetwork = branch.BranchSetup?.EnableNetwork ?? false,
            EnableBiometric = branch.BranchSetup?.EnableBiometric ?? false,
            ChildBranches = branch.ChildBranches?.Select(c => new UpdateBranchViewModel.ChildBranchInfo
            {
                BranchId = c.BranchId,
                BranchName = c.BranchName,
                BranchTypeName = c.BranchType == 1 ? "Corporate" : "Single",
                EmployeeCount = c.Employees?.Count ?? 0
            }).ToList() ?? new List<UpdateBranchViewModel.ChildBranchInfo>()
        };

        return View(viewModel);
    }

    // GET: /Account/GetAvailableParentBranches
    [HttpGet]
    public async Task<IActionResult> GetAvailableParentBranches()
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });

            // Get accessible branch IDs for the current user
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);

            // Get Corporate branches that can have children and user can access
            var availableParentBranches = await _db.Branch
                .Where(b => b.DeletedDate == null && 
                           b.BranchType == 1 && // Corporate branches only
                           accessibleBranchIds.Contains(b.BranchId))
                .OrderBy(b => b.BranchName)
                .Select(b => new { 
                    value = b.BranchId, 
                    text = b.BranchName 
                })
                .ToListAsync();

            // Add "None (Root Branch)" option for SuperAdmin users
            var options = new List<object>();
            if (await _branchAccessService.IsSuperAdminAsync(currentUser.Id))
            {
                options.Add(new { value = (int?)null, text = "None (Root Branch)" });
            }
            options.AddRange(availableParentBranches);

            return Json(new { success = true, data = options });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting available parent branches: {ex.Message}");
            return Json(new { success = false, message = "An error occurred while fetching parent branches." });
        }
    }

    // GET: /Account/GetBranchHierarchy
    [HttpGet]
    public async Task<IActionResult> GetBranchHierarchy()
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });

            var hierarchyViewModel = await GetBranchHierarchyAsync(currentUser.Id);
            
            return Json(new { success = true, data = hierarchyViewModel });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting branch hierarchy: {ex.Message}");
            return Json(new { success = false, message = "An error occurred while fetching branch hierarchy." });
        }
    }

    #region Helper Methods for Branch Hierarchy

    /// <summary>
    /// Populates available parent branches for branch creation dropdown
    /// Only Corporate branches that the user can access are included
    /// </summary>
    private async Task PopulateAvailableParentBranches(CreateBranchViewModel model, string userId)
    {
        try
        {
            // Get accessible branch IDs for the current user
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(userId);

            // Get Corporate branches that can have children and user can access
            var availableParentBranches = await _db.Branch
                .Where(b => b.DeletedDate == null && 
                           b.BranchType == 1 && // Corporate branches only
                           accessibleBranchIds.Contains(b.BranchId))
                .OrderBy(b => b.BranchName)
                .Select(b => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = b.BranchId.ToString(),
                    Text = b.BranchName,
                    Selected = b.BranchId == model.ParentBranchId
                })
                .ToListAsync();

            // Add "None (Root Branch)" option for SuperAdmin users
            if (await _branchAccessService.IsSuperAdminAsync(userId))
            {
                availableParentBranches.Insert(0, new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = "",
                    Text = "None (Root Branch)",
                    Selected = !model.ParentBranchId.HasValue
                });
            }

            model.AvailableParentBranches = availableParentBranches;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error populating available parent branches: {ex.Message}");
            model.AvailableParentBranches = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
        }
    }

    /// <summary>
    /// Gets hierarchical branch information for display purposes
    /// </summary>
    /// <param name="userId">Current user ID</param>
    /// <returns>Branch hierarchy view model</returns>
    private async Task<BranchHierarchyViewModel> GetBranchHierarchyAsync(string userId)
    {
        try
        {
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(userId);
            _logger.LogInformation($"GetBranchHierarchyAsync - UserId: {userId}, Accessible IDs: [{string.Join(", ", accessibleBranchIds)}]");

            // Get all accessible branches with their hierarchy information
            var branches = await _db.Branch
                .Where(b => b.DeletedDate == null && accessibleBranchIds.Contains(b.BranchId))
                .Include(b => b.ParentBranch)
                .Include(b => b.Employees.Where(e => e.DeletedDate == null))
                .Include(b => b.ChildBranches.Where(c => c.DeletedDate == null))
                .OrderBy(b => b.BranchName)
                .ToListAsync();

            _logger.LogInformation($"GetBranchHierarchyAsync - Retrieved {branches.Count} branches from database");

            var hierarchyViewModel = new BranchHierarchyViewModel();

            // Convert to hierarchy items
            var hierarchyItems = branches.Select(b => new BranchHierarchyItem
            {
                BranchId = b.BranchId,
                BranchName = b.BranchName,
                BranchType = b.BranchType,
                ParentBranchId = b.ParentBranchId,
                ParentBranchName = b.ParentBranch?.BranchName,
                IsSuperAdminBranch = b.IsSuperAdminBranch,
                EmployeeCount = b.Employees?.Count ?? 0,
                ChildBranchCount = b.ChildBranches?.Count ?? 0
            }).ToList();

            // Calculate hierarchy levels and paths
            foreach (var item in hierarchyItems)
            {
                item.Level = CalculateBranchLevel(item, hierarchyItems);
                item.HierarchyPath = BuildHierarchyPath(item, hierarchyItems);
            }

            hierarchyViewModel.AccessibleBranches = hierarchyItems;

            // Create select list items with proper hierarchy display
            hierarchyViewModel.BranchSelectItems = hierarchyItems
                .OrderBy(b => b.HierarchyPath)
                .Select(b => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = b.BranchId.ToString(),
                    Text = b.DisplayName
                })
                .ToList();

            // Create parent branch select items (only Corporate branches)
            hierarchyViewModel.ParentBranchSelectItems = hierarchyItems
                .Where(b => b.CanHaveChildren)
                .OrderBy(b => b.HierarchyPath)
                .Select(b => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = b.BranchId.ToString(),
                    Text = b.DisplayName
                })
                .ToList();

            return hierarchyViewModel;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting branch hierarchy: {ex.Message}");
            return new BranchHierarchyViewModel();
        }
    }

    /// <summary>
    /// Calculates the hierarchy level of a branch (0 = root, 1 = first level child, etc.)
    /// </summary>
    private int CalculateBranchLevel(BranchHierarchyItem branch, List<BranchHierarchyItem> allBranches)
    {
        if (!branch.ParentBranchId.HasValue) return 0;

        var parent = allBranches.FirstOrDefault(b => b.BranchId == branch.ParentBranchId.Value);
        if (parent == null) return 0;

        return 1 + CalculateBranchLevel(parent, allBranches);
    }

    /// <summary>
    /// Builds the full hierarchy path for a branch (e.g., "Main > Region A > Branch 1")
    /// </summary>
    private string BuildHierarchyPath(BranchHierarchyItem branch, List<BranchHierarchyItem> allBranches)
    {
        if (!branch.ParentBranchId.HasValue) return branch.BranchName;

        var parent = allBranches.FirstOrDefault(b => b.BranchId == branch.ParentBranchId.Value);
        if (parent == null) return branch.BranchName;

        return BuildHierarchyPath(parent, allBranches) + " > " + branch.BranchName;
    }

    #endregion
}
