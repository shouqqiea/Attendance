using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using LetsCheckIn.Helpers;

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

    public AccountController(
        SignInManager<ApplicationUser> signInManager, 
        UserManager<ApplicationUser> userManager, 
        ApplicationDbContext db, 
        IBranchAccessService branchAccessService,
        LetsCheckIn.Helpers.IDynamicPermissionService permissionService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _branchAccessService = branchAccessService;
        _permissionService = permissionService;
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
    public async Task<IActionResult> CreateBranch()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // SuperAdmin can always create branches, Admin needs to be checked for permissions
        var userRoles = await _userManager.GetRolesAsync(currentUser);
        if (!userRoles.Contains("SuperAdmin"))
        {
            // For Admin users, they can create branches
            if (!userRoles.Contains("Admin"))
            {
                return RedirectToAction("AccessDenied");
            }
        }

        return View(new CreateBranchViewModel { EnableLocation = true });
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

        if (!ModelState.IsValid || 
            string.IsNullOrWhiteSpace(model.AdminName) || 
            string.IsNullOrWhiteSpace(model.AdminEmail) || 
            string.IsNullOrWhiteSpace(model.BranchName) || 
            !model.EnableLocation)
        {
            if (!model.EnableLocation)
                ModelState.AddModelError("EnableLocation", "At least Location must be enabled.");
            return View(model);
        }

        // Create Admin
        var admin = new Admin { Name = model.AdminName, Email = model.AdminEmail };
        _db.Admin.Add(admin);
        await _db.SaveChangesAsync();

        // Check if this is the first branch being created
        var isFirstBranch = !await _db.Branch.AnyAsync();

        // Create Branch
        var branch = new Branch
        {
            BranchName = model.BranchName,
            AdminId = admin.AdminId,
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
        
        // Get all available roles from accessible branches
        var availableRoles = new HashSet<string>();
        foreach (var branchId in accessibleBranchIds)
        {
            var branchRoles = await _permissionService.GetRolesForBranchAsync(branchId);
            foreach (var role in branchRoles)
            {
                availableRoles.Add(role.RoleName);
            }
        }
        ViewBag.Roles = availableRoles.OrderBy(r => r).ToList();
        
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
            
            // Get all available roles from accessible branches
            var availableRoles = new HashSet<string>();
            foreach (var branchId in accessibleBranchIds)
            {
                var branchRoles = await _permissionService.GetRolesForBranchAsync(branchId);
                foreach (var role in branchRoles)
                {
                    availableRoles.Add(role.RoleName);
                }
            }
            ViewBag.Roles = availableRoles.OrderBy(r => r).ToList();
            
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
            
            var availableRoles = new HashSet<string>();
            foreach (var branchId in accessibleBranchIds)
            {
                var branchRoles = await _permissionService.GetRolesForBranchAsync(branchId);
                foreach (var role in branchRoles)
                {
                    availableRoles.Add(role.RoleName);
                }
            }
            ViewBag.Roles = availableRoles.OrderBy(r => r).ToList();
            
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
            
            var availableRoles = new HashSet<string>();
            foreach (var branchId in accessibleBranchIds)
            {
                var branchRoles = await _permissionService.GetRolesForBranchAsync(branchId);
                foreach (var role in branchRoles)
                {
                    availableRoles.Add(role.RoleName);
                }
            }
            ViewBag.Roles = availableRoles.OrderBy(r => r).ToList();
            
            return View(model);
        }

        if (!await _branchAccessService.CanAssignRoleAsync(currentUser.Id, model.Role, model.BranchId))
        {
            ModelState.AddModelError("", "You don't have permission to assign this role.");
            var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
            ViewBag.Branches = await _db.Branch
                .Where(b => accessibleBranchIds.Contains(b.BranchId))
                .ToListAsync();
            
            var availableRoles = new HashSet<string>();
            foreach (var branchId in accessibleBranchIds)
            {
                var branchRoles = await _permissionService.GetRolesForBranchAsync(branchId);
                foreach (var role in branchRoles)
                {
                    availableRoles.Add(role.RoleName);
                }
            }
            ViewBag.Roles = availableRoles.OrderBy(r => r).ToList();
            
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
            
            var availableRoles = new HashSet<string>();
            foreach (var branchId in accessibleBranchIds)
            {
                var branchRoles = await _permissionService.GetRolesForBranchAsync(branchId);
                foreach (var role in branchRoles)
                {
                    availableRoles.Add(role.RoleName);
                }
            }
            ViewBag.Roles = availableRoles.OrderBy(r => r).ToList();
            
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

        TempData["Success"] = "User created successfully.";
        return RedirectToAction("CreateUser");
    }

    // GET: /Account/BranchList
    public async Task<IActionResult> BranchList(string? search)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);

        var branches = _db.Branch
            .Include(b => b.Admin)
            .Include(b => b.BranchSetup)
            .Where(b => b.DeletedDate == null && accessibleBranchIds.Contains(b.BranchId))  // Only show accessible branches
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            branches = branches.Where(b => 
                b.BranchName.Contains(search) || 
                (b.Admin != null && b.Admin.Name.Contains(search)) ||
                (b.Admin != null && b.Admin.Email.Contains(search))
            );
        }
        return View(branches.ToList());
    }

    // POST: /Account/DeleteBranch
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBranch([FromBody] int id)
    {
        try
        {
            var branch = await _db.Branch
                .Include(b => b.BranchSetup)
                .FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
            {
                return Json(new { success = false, message = $"Branch with ID {id} not found." });
            }

            // Soft delete the branch and its setup
            branch.DeletedDate = DateTime.UtcNow;
            if (branch.BranchSetup != null)
            {
                branch.BranchSetup.DeletedDate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Branch deleted successfully." });
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBranch([FromBody] UpdateBranchViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Please check the form for errors.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            var branch = await _db.Branch
                .Include(b => b.Admin)
                .Include(b => b.BranchSetup)
                .FirstOrDefaultAsync(b => b.BranchId == model.BranchId);

            if (branch == null)
            {
                return Json(new { success = false, message = $"Branch with ID {model.BranchId} not found." });
            }

            // Update branch name
            branch.BranchName = model.BranchName;

            // Update admin details
            if (branch.Admin != null)
            {
                branch.Admin.Name = model.AdminName;
                branch.Admin.Email = model.AdminEmail;
            }

            // Update branch setup
            if (branch.BranchSetup != null)
            {
                branch.BranchSetup.EnableLocation = model.EnableLocation;
                branch.BranchSetup.EnableNetwork = model.EnableNetwork;
                branch.BranchSetup.EnableBiometric = model.EnableBiometric;
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Branch updated successfully." });
        }
        catch (Exception ex)
        {
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
        var branch = await _db.Branch
            .Include(b => b.Admin)
            .Include(b => b.BranchSetup)
            .FirstOrDefaultAsync(b => b.BranchId == id && b.DeletedDate == null);

        if (branch == null)
        {
            return NotFound("Branch not found.");
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

        return View(viewModel);
    }
}
