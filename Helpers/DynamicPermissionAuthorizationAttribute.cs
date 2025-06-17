using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using LetsCheckIn.Models;

namespace LetsCheckIn.Helpers
{
    /// <summary>
    /// Custom authorization attribute that uses permissions instead of role names
    /// This provides more flexible access control based on what users can actually do
    /// </summary>
    public class DynamicPermissionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string[] _requiredPermissions;
        private readonly bool _requireAllPermissions;

        /// <summary>
        /// Initialize with required permissions
        /// </summary>
        /// <param name="permissions">Comma-separated list of required permissions</param>
        /// <param name="requireAllPermissions">If true, user must have ALL permissions. If false, user needs ANY permission (default)</param>
        public DynamicPermissionAuthorizeAttribute(string permissions, bool requireAllPermissions = false)
        {
            _requiredPermissions = permissions.Split(',').Select(p => p.Trim()).ToArray();
            _requireAllPermissions = requireAllPermissions;
        }

        /// <summary>
        /// Initialize with permission array
        /// </summary>
        /// <param name="permissions">Array of required permissions</param>
        /// <param name="requireAllPermissions">If true, user must have ALL permissions. If false, user needs ANY permission (default)</param>
        public DynamicPermissionAuthorizeAttribute(string[] permissions, bool requireAllPermissions = false)
        {
            _requiredPermissions = permissions;
            _requireAllPermissions = requireAllPermissions;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            // Get services from DI container
            var permissionService = context.HttpContext.RequestServices
                .GetRequiredService<IDynamicPermissionService>();
            var userManager = context.HttpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<DynamicPermissionAuthorizeAttribute>>();

            try
            {
                // Get current user
                var user = await userManager.GetUserAsync(context.HttpContext.User);
                if (user == null)
                {
                    logger.LogWarning("User authentication failed - user not found");
                    context.Result = new ChallengeResult();
                    return;
                }

                // Get user's permissions
                var userPermissions = await permissionService.GetUserPermissionsAsync(user.Id);
                var userPermissionSet = userPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

                logger.LogDebug($"User {user.Email} has permissions: [{string.Join(", ", userPermissions)}]");
                logger.LogDebug($"Required permissions: [{string.Join(", ", _requiredPermissions)}], RequireAll: {_requireAllPermissions}");

                // Check permission authorization
                bool hasAccess;
                if (_requireAllPermissions)
                {
                    // User must have ALL required permissions
                    hasAccess = _requiredPermissions.All(permission => userPermissionSet.Contains(permission));
                    logger.LogDebug($"Checking ALL permissions - Result: {hasAccess}");
                }
                else
                {
                    // User must have ANY of the required permissions
                    hasAccess = _requiredPermissions.Any(permission => userPermissionSet.Contains(permission));
                    logger.LogDebug($"Checking ANY permissions - Result: {hasAccess}");
                }

                if (!hasAccess)
                {
                    logger.LogWarning($"Access denied for user {user.Email}. Required permissions: [{string.Join(", ", _requiredPermissions)}], User has: [{string.Join(", ", userPermissions)}]");
                    context.Result = new ForbidResult();
                    return;
                }

                logger.LogDebug($"Access granted for user {user.Email}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during dynamic permission authorization: {ex.Message}");
                context.Result = new StatusCodeResult(500);
            }
        }
    }

    /// <summary>
    /// Extension methods for easier permission checking
    /// </summary>
    public static class DynamicPermissionExtensions
    {
        /// <summary>
        /// Check if user has any of the specified permissions
        /// </summary>
        public static async Task<bool> HasAnyPermissionAsync(this ApplicationUser user, 
            IDynamicPermissionService permissionService, 
            params string[] permissions)
        {
            var userPermissions = await permissionService.GetUserPermissionsAsync(user.Id);
            var userPermissionSet = userPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return permissions.Any(permission => userPermissionSet.Contains(permission));
        }

        /// <summary>
        /// Check if user has all of the specified permissions
        /// </summary>
        public static async Task<bool> HasAllPermissionsAsync(this ApplicationUser user,
            IDynamicPermissionService permissionService,
            params string[] permissions)
        {
            var userPermissions = await permissionService.GetUserPermissionsAsync(user.Id);
            var userPermissionSet = userPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return permissions.All(permission => userPermissionSet.Contains(permission));
        }
    }
} 