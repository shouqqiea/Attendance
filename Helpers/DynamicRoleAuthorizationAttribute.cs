using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using LetsCheckIn.Models;

namespace LetsCheckIn.Helpers
{
    /// <summary>
    /// Custom authorization attribute that uses Dynamic roles instead of ASP.NET Identity roles
    /// </summary>
    public class DynamicRoleAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string[] _requiredRoles;
        private readonly bool _requireAllRoles;

        /// <summary>
        /// Initialize with required roles
        /// </summary>
        /// <param name="roles">Comma-separated list of required roles</param>
        /// <param name="requireAllRoles">If true, user must have ALL roles. If false, user needs ANY role (default)</param>
        public DynamicRoleAuthorizeAttribute(string roles, bool requireAllRoles = false)
        {
            _requiredRoles = roles.Split(',').Select(r => r.Trim()).ToArray();
            _requireAllRoles = requireAllRoles;
        }

        /// <summary>
        /// Initialize with role array
        /// </summary>
        /// <param name="roles">Array of required roles</param>
        /// <param name="requireAllRoles">If true, user must have ALL roles. If false, user needs ANY role (default)</param>
        public DynamicRoleAuthorizeAttribute(string[] roles, bool requireAllRoles = false)
        {
            _requiredRoles = roles;
            _requireAllRoles = requireAllRoles;
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
                .GetRequiredService<ILogger<DynamicRoleAuthorizeAttribute>>();

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

                // Get user's dynamic roles
                var userRoles = await permissionService.GetUserRolesAsync(user.Id);
                var userRoleNames = userRoles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);

                logger.LogDebug($"User {user.Email} has dynamic roles: [{string.Join(", ", userRoleNames)}]");
                logger.LogDebug($"Required roles: [{string.Join(", ", _requiredRoles)}], RequireAll: {_requireAllRoles}");

                // Check role authorization
                bool hasAccess;
                if (_requireAllRoles)
                {
                    // User must have ALL required roles
                    hasAccess = _requiredRoles.All(role => userRoleNames.Contains(role));
                    logger.LogDebug($"Checking ALL roles - Result: {hasAccess}");
                }
                else
                {
                    // User must have ANY of the required roles
                    hasAccess = _requiredRoles.Any(role => userRoleNames.Contains(role));
                    logger.LogDebug($"Checking ANY roles - Result: {hasAccess}");
                }

                if (!hasAccess)
                {
                    logger.LogWarning($"Access denied for user {user.Email}. Required: [{string.Join(", ", _requiredRoles)}], User has: [{string.Join(", ", userRoleNames)}]");
                    context.Result = new ForbidResult();
                    return;
                }

                logger.LogDebug($"Access granted for user {user.Email}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during dynamic role authorization: {ex.Message}");
                context.Result = new StatusCodeResult(500);
            }
        }
    }

    /// <summary>
    /// Extension methods for easier role checking
    /// </summary>
    public static class DynamicRoleExtensions
    {
        /// <summary>
        /// Check if user has any of the specified dynamic roles
        /// </summary>
        public static async Task<bool> HasAnyDynamicRoleAsync(this ApplicationUser user, 
            IDynamicPermissionService permissionService, 
            params string[] roles)
        {
            var userRoles = await permissionService.GetUserRolesAsync(user.Id);
            var userRoleNames = userRoles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return roles.Any(role => userRoleNames.Contains(role));
        }

        /// <summary>
        /// Check if user has all of the specified dynamic roles
        /// </summary>
        public static async Task<bool> HasAllDynamicRolesAsync(this ApplicationUser user,
            IDynamicPermissionService permissionService,
            params string[] roles)
        {
            var userRoles = await permissionService.GetUserRolesAsync(user.Id);
            var userRoleNames = userRoles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return roles.All(role => userRoleNames.Contains(role));
        }
    }
} 