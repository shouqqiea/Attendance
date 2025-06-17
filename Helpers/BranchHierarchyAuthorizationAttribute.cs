using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using LetsCheckIn.Models;

namespace LetsCheckIn.Helpers
{
    /// <summary>
    /// Authorization attribute for branch hierarchy operations
    /// Validates access based on branch hierarchy rules and user permissions
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class BranchHierarchyAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permission;
        private readonly string _branchIdParameter;
        private readonly bool _requireHierarchyValidation;

        /// <summary>
        /// Creates a branch hierarchy authorization attribute
        /// </summary>
        /// <param name="permission">Required permission for the operation</param>
        /// <param name="branchIdParameter">Name of the parameter containing branch ID (default: "branchId")</param>
        /// <param name="requireHierarchyValidation">Whether to validate hierarchy access rules</param>
        public BranchHierarchyAuthorizeAttribute(string permission, string branchIdParameter = "branchId", bool requireHierarchyValidation = true)
        {
            _permission = permission;
            _branchIdParameter = branchIdParameter;
            _requireHierarchyValidation = requireHierarchyValidation;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var services = context.HttpContext.RequestServices;
            var permissionService = services.GetRequiredService<IDynamicPermissionService>();
            var hierarchySecurityService = services.GetRequiredService<IHierarchySecurityService>();
            var logger = services.GetRequiredService<ILogger<BranchHierarchyAuthorizeAttribute>>();

            try
            {
                var userId = context.HttpContext.User.FindFirst("sub")?.Value ?? 
                           context.HttpContext.User.FindFirst("id")?.Value ??
                           context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    logger.LogWarning("User ID not found in claims");
                    context.Result = new ForbidResult();
                    return;
                }

                // Check basic permission
                var hasPermission = await permissionService.HasPermissionAsync(userId, _permission);
                if (!hasPermission)
                {
                    await hierarchySecurityService.LogSecurityEventAsync(userId, "PERMISSION_DENIED", "Authorization", 0,
                        $"Missing permission: {_permission}", true);
                    context.Result = new ForbidResult();
                    return;
                }

                // If hierarchy validation is required, check branch access
                if (_requireHierarchyValidation)
                {
                    var branchId = ExtractBranchId(context);
                    if (branchId.HasValue)
                    {
                        var hasAccess = await hierarchySecurityService.ValidateBranchAccessAsync(userId, branchId.Value);
                        if (!hasAccess)
                        {
                            await hierarchySecurityService.LogSecurityEventAsync(userId, "BRANCH_ACCESS_DENIED", "Branch", branchId.Value,
                                $"Permission: {_permission}", true);
                            context.Result = new ForbidResult();
                            return;
                        }
                    }
                }

                // Log successful authorization
                await hierarchySecurityService.LogSecurityEventAsync(userId, "AUTHORIZATION_SUCCESS", "Permission", 0,
                    $"Permission: {_permission}, Hierarchy check: {_requireHierarchyValidation}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during branch hierarchy authorization");
                context.Result = new StatusCodeResult(500);
            }
        }

        /// <summary>
        /// Extracts branch ID from request parameters
        /// </summary>
        private int? ExtractBranchId(AuthorizationFilterContext context)
        {
            // Try route values first
            if (context.RouteData.Values.TryGetValue(_branchIdParameter, out var routeValue))
            {
                if (int.TryParse(routeValue?.ToString(), out var routeBranchId))
                {
                    return routeBranchId;
                }
            }

            // Try query parameters
            if (context.HttpContext.Request.Query.TryGetValue(_branchIdParameter, out var queryValue))
            {
                if (int.TryParse(queryValue.FirstOrDefault(), out var queryBranchId))
                {
                    return queryBranchId;
                }
            }

            // Try form parameters for POST requests
            if (context.HttpContext.Request.HasFormContentType)
            {
                if (context.HttpContext.Request.Form.TryGetValue(_branchIdParameter, out var formValue))
                {
                    if (int.TryParse(formValue.FirstOrDefault(), out var formBranchId))
                    {
                        return formBranchId;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Authorization attribute for cross-branch operations
    /// Validates that users can perform operations across multiple branches
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class CrossBranchAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permission;
        private readonly string _sourceBranchParameter;
        private readonly string _targetBranchParameter;

        public CrossBranchAuthorizeAttribute(string permission, string sourceBranchParameter = "sourceBranchId", string targetBranchParameter = "targetBranchId")
        {
            _permission = permission;
            _sourceBranchParameter = sourceBranchParameter;
            _targetBranchParameter = targetBranchParameter;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var services = context.HttpContext.RequestServices;
            var permissionService = services.GetRequiredService<IDynamicPermissionService>();
            var hierarchySecurityService = services.GetRequiredService<IHierarchySecurityService>();
            var logger = services.GetRequiredService<ILogger<CrossBranchAuthorizeAttribute>>();

            try
            {
                var userId = context.HttpContext.User.FindFirst("sub")?.Value ?? 
                           context.HttpContext.User.FindFirst("id")?.Value ??
                           context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    context.Result = new ForbidResult();
                    return;
                }

                // Check basic permission
                var hasPermission = await permissionService.HasPermissionAsync(userId, _permission);
                if (!hasPermission)
                {
                    context.Result = new ForbidResult();
                    return;
                }

                // Extract branch IDs
                var sourceBranchId = ExtractBranchId(context, _sourceBranchParameter);
                var targetBranchId = ExtractBranchId(context, _targetBranchParameter);

                if (sourceBranchId.HasValue && targetBranchId.HasValue)
                {
                    var canPerformCrossBranchOperation = await hierarchySecurityService
                        .CanPerformCrossBranchOperationAsync(userId, sourceBranchId.Value, targetBranchId.Value);

                    if (!canPerformCrossBranchOperation)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during cross-branch authorization");
                context.Result = new StatusCodeResult(500);
            }
        }

        private int? ExtractBranchId(AuthorizationFilterContext context, string parameterName)
        {
            // Try route values first
            if (context.RouteData.Values.TryGetValue(parameterName, out var routeValue))
            {
                if (int.TryParse(routeValue?.ToString(), out var routeBranchId))
                {
                    return routeBranchId;
                }
            }

            // Try query parameters
            if (context.HttpContext.Request.Query.TryGetValue(parameterName, out var queryValue))
            {
                if (int.TryParse(queryValue.FirstOrDefault(), out var queryBranchId))
                {
                    return queryBranchId;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Authorization attribute for bulk operations on multiple branches
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class BulkBranchAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _permission;
        private readonly string _branchIdsParameter;

        public BulkBranchAuthorizeAttribute(string permission, string branchIdsParameter = "branchIds")
        {
            _permission = permission;
            _branchIdsParameter = branchIdsParameter;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var services = context.HttpContext.RequestServices;
            var permissionService = services.GetRequiredService<IDynamicPermissionService>();
            var hierarchySecurityService = services.GetRequiredService<IHierarchySecurityService>();
            var logger = services.GetRequiredService<ILogger<BulkBranchAuthorizeAttribute>>();

            try
            {
                var userId = context.HttpContext.User.FindFirst("sub")?.Value ?? 
                           context.HttpContext.User.FindFirst("id")?.Value ??
                           context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    context.Result = new ForbidResult();
                    return;
                }

                // Check basic permission
                var hasPermission = await permissionService.HasPermissionAsync(userId, _permission);
                if (!hasPermission)
                {
                    context.Result = new ForbidResult();
                    return;
                }

                // Extract branch IDs array
                var branchIds = ExtractBranchIds(context);
                if (branchIds?.Length > 0)
                {
                    var canAccessAll = await hierarchySecurityService.ValidateBranchSelectionAsync(userId, branchIds);
                    if (!canAccessAll)
                    {
                        context.Result = new ForbidResult();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during bulk branch authorization");
                context.Result = new StatusCodeResult(500);
            }
        }

        private int[]? ExtractBranchIds(AuthorizationFilterContext context)
        {
            // Try query parameters (comma-separated)
            if (context.HttpContext.Request.Query.TryGetValue(_branchIdsParameter, out var queryValue))
            {
                var branchIdStrings = queryValue.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
                var branchIds = new List<int>();
                
                foreach (var branchIdString in branchIdStrings)
                {
                    if (int.TryParse(branchIdString.Trim(), out var branchId))
                    {
                        branchIds.Add(branchId);
                    }
                }
                
                return branchIds.ToArray();
            }

            return null;
        }
    }
} 