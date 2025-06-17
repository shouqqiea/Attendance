using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using System.Security.Claims;

namespace LetsCheckIn.Helpers
{
    /// <summary>
    /// Interface for hierarchy-aware security operations
    /// </summary>
    public interface IHierarchySecurityService
    {
        // Core Security Validation
        Task<bool> ValidateBranchAccessAsync(string userId, int branchId);
        Task<bool> ValidateBranchSelectionAsync(string userId, int[] branchIds);
        Task<bool> ValidateDataExportPermissionAsync(string userId, int branchId);
        Task<bool> ValidateRoleAssignmentPermissionAsync(string userId, int roleId, int targetBranchId);
        
        // Cross-Branch Operation Security
        Task<bool> CanPerformCrossBranchOperationAsync(string userId, int sourceBranchId, int targetBranchId);
        Task<bool> ValidateHierarchyBoundaryAsync(string userId, int branchId);
        Task<List<int>> GetSecureAccessibleBranchIdsAsync(string userId);
        
        // Data Filtering Helpers
        Task<IQueryable<Employee>> FilterEmployeesByAccessibleBranchesAsync(IQueryable<Employee> query, string userId);
        Task<IQueryable<LeaveRequest>> FilterLeaveRequestsByAccessibleBranchesAsync(IQueryable<LeaveRequest> query, string userId);
        Task<IQueryable<Branch>> FilterBranchesByAccessibilityAsync(IQueryable<Branch> query, string userId);
        
        // Audit and Logging
        Task LogSecurityEventAsync(string userId, string action, string resourceType, int resourceId, string details = "", bool isViolation = false);
        Task LogCrossBranchAccessAttemptAsync(string userId, int sourceBranchId, int targetBranchId, bool success);
        Task LogDataExportEventAsync(string userId, string exportType, int[] branchIds, int recordCount);
        
        // Validation Helpers
        Task<ValidationResult> ValidateHierarchyConsistencyAsync();
        Task<ValidationResult> ValidateUserBranchAssignmentsAsync();
        Task<List<SecurityViolation>> DetectSecurityViolationsAsync();
    }

    /// <summary>
    /// Represents a validation result for hierarchy operations
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a security violation detected in the system
    /// </summary>
    public class SecurityViolation
    {
        public string ViolationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
    }

    /// <summary>
    /// Comprehensive security service for branch hierarchy operations
    /// </summary>
    public class HierarchySecurityService : IHierarchySecurityService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBranchAccessService _branchAccessService;
        private readonly IDynamicPermissionService _permissionService;
        private readonly ILogger<HierarchySecurityService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HierarchySecurityService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBranchAccessService branchAccessService,
            IDynamicPermissionService permissionService,
            ILogger<HierarchySecurityService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
            _branchAccessService = branchAccessService;
            _permissionService = permissionService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        #region Core Security Validation

        /// <summary>
        /// Validates if a user can access a specific branch with comprehensive security checks
        /// </summary>
        public async Task<bool> ValidateBranchAccessAsync(string userId, int branchId)
        {
            try
            {
                // Basic access check
                var hasAccess = await _branchAccessService.CanAccessBranchAsync(userId, branchId);
                
                // Log access attempt
                await LogSecurityEventAsync(userId, "BRANCH_ACCESS_ATTEMPT", "Branch", branchId, 
                    $"Access result: {hasAccess}");

                // Additional security checks for suspicious patterns
                if (hasAccess)
                {
                    await ValidateAccessPatternAsync(userId, branchId);
                }

                return hasAccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating branch access for user {UserId} to branch {BranchId}", userId, branchId);
                await LogSecurityEventAsync(userId, "BRANCH_ACCESS_ERROR", "Branch", branchId, 
                    $"Error: {ex.Message}", true);
                return false;
            }
        }

        /// <summary>
        /// Validates multiple branch selections for bulk operations
        /// </summary>
        public async Task<bool> ValidateBranchSelectionAsync(string userId, int[] branchIds)
        {
            if (branchIds?.Length == 0) return true;

            var accessibleBranchIds = await GetSecureAccessibleBranchIdsAsync(userId);
            var invalidBranches = branchIds.Where(id => !accessibleBranchIds.Contains(id)).ToArray();

            if (invalidBranches.Any())
            {
                await LogSecurityEventAsync(userId, "INVALID_BRANCH_SELECTION", "BranchSelection", 0,
                    $"Attempted to access unauthorized branches: {string.Join(", ", invalidBranches)}", true);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates data export permissions with audit logging
        /// </summary>
        public async Task<bool> ValidateDataExportPermissionAsync(string userId, int branchId)
        {
            // Check both branch access and export permission
            var hasAccess = await ValidateBranchAccessAsync(userId, branchId);
            var hasPermission = await _permissionService.HasPermissionAsync(userId, "data.export");

            var canExport = hasAccess && hasPermission;

            await LogSecurityEventAsync(userId, "DATA_EXPORT_PERMISSION_CHECK", "Branch", branchId,
                $"Export permission result: {canExport} (Access: {hasAccess}, Permission: {hasPermission})");

            return canExport;
        }

        /// <summary>
        /// Validates role assignment permissions based on hierarchy rules
        /// </summary>
        public async Task<bool> ValidateRoleAssignmentPermissionAsync(string userId, int roleId, int targetBranchId)
        {
            // Check if user can access target branch
            var canAccessBranch = await ValidateBranchAccessAsync(userId, targetBranchId);
            if (!canAccessBranch) return false;

            // Check if user can assign roles
            var canAssignRoles = await _permissionService.HasPermissionAsync(userId, "role.assign");
            if (!canAssignRoles) return false;

            // Check role-specific assignment permissions
            var canAssignRole = await _permissionService.CanAssignRoleToBranchAsync(userId, roleId, targetBranchId);

            await LogSecurityEventAsync(userId, "ROLE_ASSIGNMENT_VALIDATION", "Role", roleId,
                $"Target Branch: {targetBranchId}, Can Assign: {canAssignRole}");

            return canAssignRole;
        }

        #endregion

        #region Cross-Branch Operation Security

        /// <summary>
        /// Validates cross-branch operations with enhanced security checks
        /// </summary>
        public async Task<bool> CanPerformCrossBranchOperationAsync(string userId, int sourceBranchId, int targetBranchId)
        {
            // Both branches must be accessible
            var canAccessSource = await ValidateBranchAccessAsync(userId, sourceBranchId);
            var canAccessTarget = await ValidateBranchAccessAsync(userId, targetBranchId);

            var canPerform = canAccessSource && canAccessTarget;

            await LogCrossBranchAccessAttemptAsync(userId, sourceBranchId, targetBranchId, canPerform);

            return canPerform;
        }

        /// <summary>
        /// Validates that operations respect hierarchy boundaries
        /// </summary>
        public async Task<bool> ValidateHierarchyBoundaryAsync(string userId, int branchId)
        {
            var userBranch = await _branchAccessService.GetUserBranchAsync(userId);
            if (userBranch == null) return false;

            // SuperAdmin can cross all boundaries
            if (await _branchAccessService.IsSuperAdminAsync(userId)) return true;

            // Check if branch is within user's hierarchy scope
            var accessibleBranchIds = await GetSecureAccessibleBranchIdsAsync(userId);
            
            return accessibleBranchIds.Contains(branchId);
        }

        /// <summary>
        /// Gets accessible branch IDs with comprehensive security validation
        /// </summary>
        public async Task<List<int>> GetSecureAccessibleBranchIdsAsync(string userId)
        {
            try
            {
                var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(userId);
                
                // Validate each branch access (additional security layer)
                var validatedBranchIds = new List<int>();
                foreach (var branchId in accessibleBranchIds)
                {
                    if (await _branchAccessService.CanAccessBranchAsync(userId, branchId))
                    {
                        validatedBranchIds.Add(branchId);
                    }
                    else
                    {
                        // Log potential security issue
                        await LogSecurityEventAsync(userId, "BRANCH_ACCESS_INCONSISTENCY", "Branch", branchId,
                            "Branch listed as accessible but failed validation", true);
                    }
                }

                return validatedBranchIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting secure accessible branch IDs for user {UserId}", userId);
                return new List<int>();
            }
        }

        #endregion

        #region Data Filtering Helpers

        /// <summary>
        /// Filters employee queries by accessible branches with security validation
        /// </summary>
        public async Task<IQueryable<Employee>> FilterEmployeesByAccessibleBranchesAsync(IQueryable<Employee> query, string userId)
        {
            var accessibleBranchIds = await GetSecureAccessibleBranchIdsAsync(userId);
            
            await LogSecurityEventAsync(userId, "EMPLOYEE_DATA_FILTER", "Query", 0,
                $"Filtering by branches: {string.Join(", ", accessibleBranchIds)}");

            return query.Where(e => accessibleBranchIds.Contains(e.BranchId) && e.DeletedDate == null);
        }

        /// <summary>
        /// Filters leave request queries by accessible branches with security validation
        /// </summary>
        public async Task<IQueryable<LeaveRequest>> FilterLeaveRequestsByAccessibleBranchesAsync(IQueryable<LeaveRequest> query, string userId)
        {
            var accessibleBranchIds = await GetSecureAccessibleBranchIdsAsync(userId);
            
            await LogSecurityEventAsync(userId, "LEAVE_REQUEST_DATA_FILTER", "Query", 0,
                $"Filtering by branches: {string.Join(", ", accessibleBranchIds)}");

            return query.Where(lr => lr.Employee != null && accessibleBranchIds.Contains(lr.Employee.BranchId));
        }

        /// <summary>
        /// Filters branch queries by accessibility with security validation
        /// </summary>
        public async Task<IQueryable<Branch>> FilterBranchesByAccessibilityAsync(IQueryable<Branch> query, string userId)
        {
            var accessibleBranchIds = await GetSecureAccessibleBranchIdsAsync(userId);
            
            await LogSecurityEventAsync(userId, "BRANCH_DATA_FILTER", "Query", 0,
                $"Filtering by accessible branches: {string.Join(", ", accessibleBranchIds)}");

            return query.Where(b => accessibleBranchIds.Contains(b.BranchId) && b.DeletedDate == null);
        }

        #endregion

        #region Audit and Logging

        /// <summary>
        /// Logs security events for audit purposes
        /// </summary>
        public async Task LogSecurityEventAsync(string userId, string action, string resourceType, int resourceId, string details = "", bool isViolation = false)
        {
            try
            {
                var securityLog = new SecurityAuditLog
                {
                    UserId = userId,
                    Action = action,
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    Details = details,
                    IPAddress = GetCurrentIPAddress(),
                    UserAgent = GetCurrentUserAgent(),
                    HttpMethod = GetCurrentHttpMethod(),
                    RequestPath = GetCurrentRequestPath(),
                    SessionId = GetCurrentSessionId(),
                    IsSecurityViolation = isViolation,
                    Timestamp = DateTime.UtcNow,
                    Success = !isViolation
                };

                _context.SecurityAuditLogs.Add(securityLog);
                await _context.SaveChangesAsync();

                var logLevel = isViolation ? LogLevel.Warning : LogLevel.Information;
                _logger.Log(logLevel, "Security event logged: {Action} by user {UserId} on {ResourceType} {ResourceId}",
                    action, userId, resourceType, resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log security event for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Logs cross-branch access attempts for security monitoring
        /// </summary>
        public async Task LogCrossBranchAccessAttemptAsync(string userId, int sourceBranchId, int targetBranchId, bool success)
        {
            await LogSecurityEventAsync(userId, "CROSS_BRANCH_ACCESS", "BranchPair", sourceBranchId,
                $"Source: {sourceBranchId}, Target: {targetBranchId}, Success: {success}", !success);
        }

        /// <summary>
        /// Logs data export events for compliance
        /// </summary>
        public async Task LogDataExportEventAsync(string userId, string exportType, int[] branchIds, int recordCount)
        {
            await LogSecurityEventAsync(userId, "DATA_EXPORT", "Export", 0,
                $"Type: {exportType}, Branches: [{string.Join(", ", branchIds)}], Records: {recordCount}");
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validates hierarchy consistency across the system
        /// </summary>
        public async Task<ValidationResult> ValidateHierarchyConsistencyAsync()
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // Check for circular references
                var branches = await _context.Branch
                    .Where(b => b.DeletedDate == null)
                    .ToListAsync();

                foreach (var branch in branches)
                {
                    if (await HasCircularReferenceAsync(branch.BranchId, branches))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Circular reference detected for branch {branch.BranchId} ({branch.BranchName})");
                    }
                }

                // Check for orphaned branches
                var orphanedBranches = branches
                    .Where(b => b.ParentBranchId.HasValue && 
                               !branches.Any(p => p.BranchId == b.ParentBranchId.Value))
                    .ToList();

                foreach (var orphan in orphanedBranches)
                {
                    result.Warnings.Add($"Orphaned branch detected: {orphan.BranchId} ({orphan.BranchName}) - Parent {orphan.ParentBranchId} not found");
                }

                // Check branch type consistency
                var invalidHierarchies = branches
                    .Where(b => b.ParentBranchId.HasValue)
                    .Join(branches, 
                        child => child.ParentBranchId, 
                        parent => parent.BranchId,
                        (child, parent) => new { Child = child, Parent = parent })
                    .Where(x => !x.Parent.IsCorporateBranch)
                    .ToList();

                foreach (var invalid in invalidHierarchies)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Invalid hierarchy: Single branch {invalid.Parent.BranchId} ({invalid.Parent.BranchName}) has child branch {invalid.Child.BranchId} ({invalid.Child.BranchName})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating hierarchy consistency");
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validates user branch assignments for security compliance
        /// </summary>
        public async Task<ValidationResult> ValidateUserBranchAssignmentsAsync()
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // Check for users without branch assignments
                var usersWithoutBranches = await _context.Users
                    .Where(u => !_context.Employee.Any(e => e.UserId == u.Id && e.DeletedDate == null))
                    .ToListAsync();

                foreach (var user in usersWithoutBranches)
                {
                    result.Warnings.Add($"User {user.Id} ({user.Email}) has no branch assignment");
                }

                // Check for users assigned to deleted branches
                var usersWithDeletedBranches = await _context.Employee
                    .Include(e => e.Branch)
                    .Where(e => e.DeletedDate == null && e.Branch!.DeletedDate != null)
                    .ToListAsync();

                foreach (var employee in usersWithDeletedBranches)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Employee {employee.EmployeeId} is assigned to deleted branch {employee.BranchId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user branch assignments");
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Detects potential security violations in the system
        /// </summary>
        public async Task<List<SecurityViolation>> DetectSecurityViolationsAsync()
        {
            var violations = new List<SecurityViolation>();

            try
            {
                // Check for suspicious cross-branch access patterns
                var suspiciousAccess = await _context.SecurityAuditLogs
                    .Where(log => log.Action == "CROSS_BRANCH_ACCESS" && 
                                  log.Timestamp > DateTime.UtcNow.AddHours(-24))
                    .GroupBy(log => log.UserId)
                    .Where(group => group.Count() > 50) // More than 50 cross-branch accesses in 24 hours
                    .ToListAsync();

                foreach (var group in suspiciousAccess)
                {
                    violations.Add(new SecurityViolation
                    {
                        ViolationType = "EXCESSIVE_CROSS_BRANCH_ACCESS",
                        Description = $"User performed {group.Count()} cross-branch accesses in the last 24 hours",
                        UserId = group.Key,
                        Severity = "High"
                    });
                }

                // Check for failed access attempts
                var failedAccess = await _context.SecurityAuditLogs
                    .Where(log => log.Action == "BRANCH_ACCESS_ATTEMPT" && 
                                  log.Details.Contains("Access result: False") &&
                                  log.Timestamp > DateTime.UtcNow.AddHours(-1))
                    .GroupBy(log => log.UserId)
                    .Where(group => group.Count() > 10) // More than 10 failed attempts in 1 hour
                    .ToListAsync();

                foreach (var group in failedAccess)
                {
                    violations.Add(new SecurityViolation
                    {
                        ViolationType = "EXCESSIVE_FAILED_ACCESS",
                        Description = $"User had {group.Count()} failed access attempts in the last hour",
                        UserId = group.Key,
                        Severity = "Medium"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting security violations");
            }

            return violations;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Validates access patterns for anomaly detection
        /// </summary>
        private async Task ValidateAccessPatternAsync(string userId, int branchId)
        {
            try
            {
                // Check access frequency
                var recentAccess = await _context.SecurityAuditLogs
                    .Where(log => log.UserId == userId && 
                                  log.ResourceId == branchId && 
                                  log.Timestamp > DateTime.UtcNow.AddMinutes(-5))
                    .CountAsync();

                if (recentAccess > 20) // More than 20 accesses to same branch in 5 minutes
                {
                    await LogSecurityEventAsync(userId, "SUSPICIOUS_ACCESS_PATTERN", "Branch", branchId,
                        $"High frequency access: {recentAccess} times in 5 minutes", true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating access pattern for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Checks for circular references in branch hierarchy
        /// </summary>
        private async Task<bool> HasCircularReferenceAsync(int branchId, List<Branch> allBranches, HashSet<int>? visited = null)
        {
            visited ??= new HashSet<int>();

            if (visited.Contains(branchId))
                return true; // Circular reference detected

            visited.Add(branchId);

            var branch = allBranches.FirstOrDefault(b => b.BranchId == branchId);
            if (branch?.ParentBranchId == null)
                return false;

            return await HasCircularReferenceAsync(branch.ParentBranchId.Value, allBranches, visited);
        }

        /// <summary>
        /// Gets current user's IP address for audit logging
        /// </summary>
        private string GetCurrentIPAddress()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                }
            }
            catch { }
            return "Unknown";
        }

        /// <summary>
        /// Gets current user's user agent for audit logging
        /// </summary>
        private string GetCurrentUserAgent()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    return httpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
                }
            }
            catch { }
            return "Unknown";
        }

        /// <summary>
        /// Gets current HTTP method for audit logging
        /// </summary>
        private string GetCurrentHttpMethod()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    return httpContext.Request.Method;
                }
            }
            catch { }
            return "Unknown";
        }

        /// <summary>
        /// Gets current request path for audit logging
        /// </summary>
        private string GetCurrentRequestPath()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    return httpContext.Request.Path.ToString();
                }
            }
            catch { }
            return "Unknown";
        }

        /// <summary>
        /// Gets current session ID for audit logging
        /// </summary>
        private string? GetCurrentSessionId()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    return httpContext.Session?.Id;
                }
            }
            catch { }
            return null;
        }

        #endregion
    }
} 