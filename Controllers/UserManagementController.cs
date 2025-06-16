using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using System.Text.Json;
using System.Text.Json.Serialization;
using LetsCheckIn.Helpers;

namespace LetsCheckIn.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class UserManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBranchAccessService _branchAccessService;
        private readonly LetsCheckIn.Helpers.IDynamicPermissionService _permissionService;
        private readonly ILogger<UserManagementController> _logger;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserManagementController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBranchAccessService branchAccessService,
            LetsCheckIn.Helpers.IDynamicPermissionService permissionService,
            ILogger<UserManagementController> logger,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _branchAccessService = branchAccessService;
            _permissionService = permissionService;
            _logger = logger;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
                _logger.LogInformation($"User {currentUser.Id} has access to branches: {string.Join(", ", accessibleBranchIds)}");

                // Get accessible branches for the filter dropdown
                var branches = await _context.Branch
                    .Where(b => accessibleBranchIds.Contains(b.BranchId))
                    .ToListAsync();
                ViewBag.Branches = branches;

                // Get available roles for the filter dropdown
                var availableRoles = new HashSet<string>();

                // Get all Identity roles
                var identityRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                foreach (var role in identityRoles.Where(r => r != null))
                {
                    availableRoles.Add(role);
                }

                // Get dynamic roles for accessible branches
                foreach (var branchId in accessibleBranchIds)
                {
                    var branchRoles = await _permissionService.GetRolesForBranchAsync(branchId);
                    foreach (var role in branchRoles)
                    {
                        availableRoles.Add(role.RoleName);
                    }
                }

                _logger.LogInformation($"Retrieved {availableRoles.Count} total roles for user {currentUser.Id}");
                ViewBag.Roles = availableRoles.OrderBy(r => r).ToList();

                // Get users with their roles and employee info in a single query
                var users = await _context.Users
                    .Include(u => u.Employee)
                        .ThenInclude(e => e.Branch)
                    .Where(u => u.Employee != null && accessibleBranchIds.Contains(u.Employee.BranchId))
                    .Select(u => new UserListViewModel
                    {
                        Id = u.Id,
                        FullName = $"{u.Employee.FirstName} {u.Employee.LastName}",
                        Email = u.Email,
                        UserName = u.UserName,
                        BranchName = u.Employee.Branch.BranchName,
                        Roles = _context.UserRoles
                            .Where(ur => ur.UserId == u.Id)
                            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                            .ToList() ?? new List<string>(),
                        IsActive = u.Employee.Status,
                        CreatedDate = u.Employee.CreatedDate,
                        LastLoginDate = u.LastLoginDate
                    })
                    .ToListAsync();

                _logger.LogInformation($"Found {users.Count} users for the current user's accessible branches");
                
                // Log details about the specific user we're looking for
                var existingUser = await _context.Users
                    .Include(u => u.Employee)
                        .ThenInclude(e => e.Branch)
                    .FirstOrDefaultAsync(u => u.Email == "syahwar32@gmail.com");
                
                if (existingUser != null)
                {
                    _logger.LogInformation($"Found existing user {existingUser.Id} with email {existingUser.Email}");
                    if (existingUser.Employee != null)
                    {
                        _logger.LogInformation($"User is in branch {existingUser.Employee.BranchId} ({existingUser.Employee.Branch?.BranchName})");
                        _logger.LogInformation($"User's status is {(existingUser.Employee.Status ? "Active" : "Inactive")}");
                        _logger.LogInformation($"User's employee record created on {existingUser.Employee.CreatedDate}");
                    }
                    else
                    {
                        _logger.LogWarning("User exists but has no employee record");
                    }
                }

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UserManagement Index: {ex.Message}");
                throw;
            }
        }

        // GET: /UserManagement/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);
                ViewBag.Branches = await _context.Branch
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

                return View(new UserManagementViewModel());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UserManagement Create: {ex.Message}");
                throw;
            }
        }

        // POST: /UserManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] UserManagementViewModel model)
        {
            try
            {
                _logger.LogInformation($"Attempting to create user with email: {model.Email}");

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogWarning($"Validation failed: {string.Join(", ", errors)}");
                    return Json(new { success = false, message = "Validation failed", errors = errors });
                }

                // Check if user can access the target branch
                if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, model.BranchId))
                {
                    _logger.LogWarning($"User {currentUser.Id} attempted to create user in branch {model.BranchId} without permission");
                    return Json(new { success = false, message = "You don't have permission to create users in this branch" });
                }

                // Validate that the role is assigned to the branch
                var branchRoles = await _permissionService.GetRolesForBranchAsync(model.BranchId);
                var invalidRoles = model.Roles.Where(role => !branchRoles.Any(br => br.RoleName == role)).ToList();
                if (invalidRoles.Any())
                {
                    _logger.LogWarning($"Invalid roles {string.Join(", ", invalidRoles)} for branch {model.BranchId}");
                    return Json(new { success = false, message = $"The following roles are not available for this branch: {string.Join(", ", invalidRoles)}" });
                }

                // Check if user can assign the requested roles
                foreach (var role in model.Roles)
                {
                    if (!await _branchAccessService.CanAssignRoleAsync(currentUser.Id, role, model.BranchId))
                    {
                        _logger.LogWarning($"User {currentUser.Id} attempted to assign role {role} without permission");
                        return Json(new { success = false, message = $"You don't have permission to assign the role: {role}" });
                    }
                }

                // Check if email already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning($"Email {model.Email} already exists. User ID: {existingUser.Id}");
                    
                    // Check if the user is in an accessible branch
                    var existingEmployee = await _context.Employee
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => e.UserId == existingUser.Id);
                    
                    if (existingEmployee != null)
                    {
                        var canAccessBranch = await _branchAccessService.CanAccessBranchAsync(currentUser.Id, existingEmployee.BranchId);
                        _logger.LogInformation($"Existing user is in branch {existingEmployee.BranchId} ({existingEmployee.Branch?.BranchName}). Current user can access: {canAccessBranch}");
                        _logger.LogInformation($"Existing user's status is {(existingEmployee.Status ? "Active" : "Inactive")}");
                        
                        if (!canAccessBranch)
                        {
                            return Json(new { 
                                success = false, 
                                message = $"Email already exists in branch {existingEmployee.Branch?.BranchName} which you don't have access to" 
                            });
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"User {existingUser.Id} exists but has no employee record. Creating employee record...");
                        
                        // Create Employee record for existing user
                        var newEmployee = new Employee
                        {
                            UserId = existingUser.Id,
                            FirstName = model.FirstName,
                            LastName = model.LastName,
                            Email = model.Email,
                            BranchId = model.BranchId,
                            Status = model.Status,
                            CreatedDate = DateTime.UtcNow
                        };
                        _context.Employee.Add(newEmployee);

                        // Ensure roles exist in Identity and assign them
                        foreach (var role in model.Roles)
                        {
                            if (!await _roleManager.RoleExistsAsync(role))
                            {
                                _logger.LogInformation($"Creating Identity role: {role}");
                                await _roleManager.CreateAsync(new IdentityRole(role));
                            }

                            // Assign role
                            var existingRoleResult = await _userManager.AddToRoleAsync(existingUser, role);
                            if (!existingRoleResult.Succeeded)
                            {
                                _logger.LogError($"Failed to assign role {role}: {string.Join(", ", existingRoleResult.Errors.Select(e => e.Description))}");
                                return Json(new { success = false, message = $"Failed to assign role: {role}", errors = existingRoleResult.Errors.Select(e => e.Description) });
                            }

                            // Ensure role exists in DynamicRoles
                            var existingDynamicRole = await _context.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == role);
                            if (existingDynamicRole == null)
                            {
                                _logger.LogInformation($"Creating Dynamic role: {role}");
                                existingDynamicRole = await _permissionService.CreateRoleAsync(role, null, currentUser.Id);
                                if (existingDynamicRole == null)
                                {
                                    _logger.LogError($"Failed to create dynamic role: {role}");
                                    return Json(new { success = false, message = $"Failed to create dynamic role: {role}" });
                                }
                            }

                            // Assign to Dynamic Role System
                            var existingDynamicRoleResult = await _permissionService.AssignRoleToUserAsync(existingUser.Id, existingDynamicRole.RoleId, currentUser.Id);
                            if (!existingDynamicRoleResult)
                            {
                                _logger.LogWarning($"Failed to assign dynamic role {role} to user {existingUser.Id}");
                            }
                            else
                            {
                                _logger.LogInformation($"Successfully assigned dynamic role {role} (RoleId: {existingDynamicRole.RoleId}) to user {existingUser.Id}");
                            }
                        }

                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Successfully created employee record for existing user {existingUser.Id}");
                        return Json(new { success = true, message = "User account completed successfully" });
                    }
                    
                    return Json(new { success = false, message = "Email already exists" });
                }

                // Create new user
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    BranchId = model.BranchId
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    _logger.LogError($"Failed to create user: {string.Join(", ", errors)}");
                    return Json(new { success = false, message = "Failed to create user", errors = errors });
                }

                // Create Employee record
                var employee = new Employee
                {
                    UserId = user.Id,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    BranchId = model.BranchId,
                    Status = model.Status,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Employee.Add(employee);

                // Ensure roles exist and assign them
                foreach (var role in model.Roles)
                {
                    // Ensure role exists in Identity
                    if (!await _roleManager.RoleExistsAsync(role))
                    {
                        _logger.LogInformation($"Creating Identity role: {role}");
                        await _roleManager.CreateAsync(new IdentityRole(role));
                    }

                    // Ensure role exists in DynamicRoles
                    var dynamicRole = await _context.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == role);
                    if (dynamicRole == null)
                    {
                        _logger.LogInformation($"Creating Dynamic role: {role}");
                        dynamicRole = await _permissionService.CreateRoleAsync(role, null, currentUser.Id);
                        if (dynamicRole == null)
                        {
                            _logger.LogError($"Failed to create dynamic role: {role}");
                            // Clean up the created user since role creation failed
                            await _userManager.DeleteAsync(user);
                            return Json(new { success = false, message = $"Failed to create dynamic role: {role}" });
                        }
                    }

                    // Assign role in Identity
                    var roleResult = await _userManager.AddToRoleAsync(user, role);
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError($"Failed to assign role {role}: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                        // Clean up the created user since role assignment failed
                        await _userManager.DeleteAsync(user);
                        return Json(new { success = false, message = $"Failed to assign role: {role}", errors = roleResult.Errors.Select(e => e.Description) });
                    }

                    // Assign to Dynamic Role System
                    var dynamicRoleResult = await _permissionService.AssignRoleToUserAsync(user.Id, dynamicRole.RoleId, currentUser.Id);
                    if (!dynamicRoleResult)
                    {
                        _logger.LogWarning($"Failed to assign dynamic role {role} to user {user.Id}");
                    }
                    else
                    {
                        _logger.LogInformation($"Successfully assigned dynamic role {role} (RoleId: {dynamicRole.RoleId}) to user {user.Id}");
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully created user {user.Id} with roles {string.Join(", ", model.Roles)}");
                return Json(new { success = true, message = "User created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating user: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while creating the user" });
            }
        }

        // GET: /UserManagement/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            try
            {
                _logger.LogInformation($"Edit action called for user ID: {id}");
                
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });

                var user = await _userManager.Users
                    .Include(u => u.Employee)
                        .ThenInclude(e => e.Branch)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    _logger.LogWarning($"User not found with ID: {id}");
                    return Json(new { success = false, message = "User not found" });
                }

                if (user.Employee == null)
                {
                    _logger.LogWarning($"Employee record not found for user ID: {id}");
                    return Json(new { success = false, message = "Employee record not found" });
                }

                // Check if user can access this user's branch
                if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, user.Employee.BranchId))
                {
                    _logger.LogWarning($"User {currentUser.Id} attempted to edit user {id} without permission");
                    return Json(new { success = false, message = "You don't have permission to edit this user" });
                }

                // Get user roles from both Identity and Dynamic Role systems
                var identityRoles = await _userManager.GetRolesAsync(user);
                var dynamicRoles = await _context.DynamicUserRoles
                    .Where(ur => ur.UserId == user.Id)
                    .Join(_context.DynamicRoles,
                        ur => ur.RoleId,
                        r => r.RoleId,
                        (ur, r) => r.RoleName)
                    .ToListAsync();

                var combinedRoles = identityRoles.Union(dynamicRoles).ToList();
                _logger.LogInformation($"User roles - Identity: {string.Join(", ", identityRoles)}, Dynamic: {string.Join(", ", dynamicRoles)}");

                var model = new UserManagementViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.Employee.FirstName,
                    LastName = user.Employee.LastName,
                    UserName = user.UserName,
                    BranchId = user.Employee.BranchId,
                    BranchName = user.Employee.Branch?.BranchName ?? string.Empty,
                    Roles = combinedRoles,
                    Status = user.Employee.Status
                };

                _logger.LogInformation($"Returning user details for edit - ID: {model.Id}, Name: {model.FirstName} {model.LastName}, Email: {model.Email}, Username: {model.UserName}, Branch: {model.BranchId}, Role: {string.Join(", ", model.Roles)}, Status: {model.Status}");

                return Json(new { 
                    success = true, 
                    data = model 
                }, new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UserManagement Edit: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while loading user details" });
            }
        }

        // POST: /UserManagement/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromBody] UserManagementViewModel model)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });

                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Validation failed", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
                }

                var user = await _userManager.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.Id == model.Id);

                if (user == null || user.Employee == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Check permissions for current branch
                if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, user.Employee.BranchId))
                {
                    return Json(new { success = false, message = "You don't have permission to edit this user" });
                }

                // Check permissions for target branch (if changing)
                if (user.Employee.BranchId != model.BranchId)
                {
                    if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, model.BranchId))
                    {
                        return Json(new { success = false, message = "You don't have permission to move users to this branch" });
                    }
                }

                // Validate that all roles are assigned to the target branch
                var branchRoles = await _permissionService.GetRolesForBranchAsync(model.BranchId);
                var invalidRoles = model.Roles.Where(role => !branchRoles.Any(br => br.RoleName == role)).ToList();
                if (invalidRoles.Any())
                {
                    return Json(new { success = false, message = $"The following roles are not available for this branch: {string.Join(", ", invalidRoles)}" });
                }

                // Check if user can assign all the requested roles
                foreach (var role in model.Roles)
                {
                    if (!await _branchAccessService.CanAssignRoleAsync(currentUser.Id, role, model.BranchId))
                    {
                        return Json(new { success = false, message = $"You don't have permission to assign the role: {role}" });
                    }
                }

                // Update user details
                user.Email = model.Email;
                user.UserName = model.Email;

                // Start a transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try 
                {
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        return Json(new { success = false, message = "Failed to update user", errors = updateResult.Errors.Select(e => e.Description) });
                    }

                    // Update employee details
                    user.Employee.FirstName = model.FirstName;
                    user.Employee.LastName = model.LastName;
                    user.Employee.Email = model.Email;
                    user.Employee.BranchId = model.BranchId;
                    user.Employee.Status = model.Status;

                    _context.Employee.Update(user.Employee);
                    await _context.SaveChangesAsync();

                    // Update roles
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    var rolesToRemove = currentRoles.Except(model.Roles);
                    var rolesToAdd = model.Roles.Except(currentRoles);

                    if (rolesToRemove.Any())
                    {
                        await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                        // Remove from Dynamic Role System
                        foreach (var role in rolesToRemove)
                        {
                            var dynamicRole = await _context.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == role);
                            if (dynamicRole != null)
                            {
                                await _permissionService.RemoveRoleFromUserAsync(user.Id, dynamicRole.RoleId);
                            }
                        }
                    }

                    if (rolesToAdd.Any())
                    {
                        await _userManager.AddToRolesAsync(user, rolesToAdd);
                        // Add to Dynamic Role System
                        foreach (var role in rolesToAdd)
                        {
                            var dynamicRole = await _context.DynamicRoles.FirstOrDefaultAsync(r => r.RoleName == role);
                            if (dynamicRole != null)
                            {
                                await _permissionService.AssignRoleToUserAsync(user.Id, dynamicRole.RoleId, currentUser.Id);
                            }
                        }
                    }

                    // Commit the transaction
                    await transaction.CommitAsync();
                    return Json(new { success = true, message = "User updated successfully" });
                }
                catch (Exception ex)
                {
                    // Rollback on error
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error updating user: {ex.Message}");
                    return Json(new { success = false, message = "An error occurred while updating the user" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating user: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the user" });
            }
        }

        // POST: /UserManagement/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] string id)
        {
            try
            {
                _logger.LogInformation($"Attempting to delete user with ID: {id}"); // Debug log

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });

                var user = await _userManager.Users
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null || user.Employee == null)
                {
                    _logger.LogWarning($"User not found with ID: {id}"); // Debug log
                    return Json(new { success = false, message = "User not found" });
                }

                // Check if user can access this user's branch
                if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, user.Employee.BranchId))
                {
                    _logger.LogWarning($"User {currentUser.Id} attempted to delete user {id} without permission"); // Debug log
                    return Json(new { success = false, message = "You don't have permission to delete this user" });
                }

                // Remove user from all roles
                var userRoles = await _userManager.GetRolesAsync(user);
                if (userRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, userRoles);
                }

                // Remove from Dynamic Role System
                var dynamicRoles = await _context.DynamicUserRoles
                    .Where(ur => ur.UserId == id)
                    .ToListAsync();
                if (dynamicRoles.Any())
                {
                    _context.DynamicUserRoles.RemoveRange(dynamicRoles);
                }

                // Delete the employee record
                _context.Employee.Remove(user.Employee);

                // Delete the user
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    _logger.LogError($"Failed to delete user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    return Json(new { success = false, message = "Failed to delete user" });
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully deleted user with ID: {id}"); // Debug log
                return Json(new { success = true, message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting user: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the user" });
            }
        }

        // GET: /UserManagement/GetRolesForBranch
        [HttpGet]
        public async Task<IActionResult> GetRolesForBranch(int branchId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });

                // Check if user can access this branch
                if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, branchId))
                {
                    return Json(new { success = false, message = "You don't have permission to access this branch" });
                }

                // Get dynamic roles for the branch
                var branchRoles = await _permissionService.GetRolesForBranchAsync(branchId);
                var dynamicRoleNames = branchRoles.Select(r => r.RoleName).ToHashSet();

                // Get all Identity roles
                var identityRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                
                // Combine both sets of roles
                var allRoles = dynamicRoleNames.Union(identityRoles.Where(r => r != null));

                var roles = allRoles.Select(r => new { value = r, text = r }).OrderBy(r => r.text).ToList();

                _logger.LogInformation($"Retrieved {roles.Count} roles for branch {branchId}");
                
                return Json(new { success = true, roles = roles }, new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting roles for branch: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while getting roles for the branch" });
            }
        }
    }

    public class UserManagementViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public bool Status { get; set; }
        public string? Password { get; set; }
        public string UserName { get; set; } = string.Empty;
    }
} 