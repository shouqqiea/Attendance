using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;
using LetsCheckIn.Models;

namespace LetsCheckIn.Helpers
{
    public static class HtmlHelperExtensions
    {
        public static async Task<bool> HasPermission(this IHtmlHelper html, string permissionName)
        {
            var httpContext = html.ViewContext.HttpContext;
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
                return false;

            var permissionService = (LetsCheckIn.Helpers.IDynamicPermissionService)httpContext.RequestServices.GetService(typeof(LetsCheckIn.Helpers.IDynamicPermissionService));
            return await permissionService.HasPermissionAsync(userId, permissionName);
        }

        public static async Task<bool> HasAnyPermission(this IHtmlHelper html, params string[] permissions)
        {
            var httpContext = html.ViewContext.HttpContext;
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
                return false;

            var permissionService = (LetsCheckIn.Helpers.IDynamicPermissionService)httpContext.RequestServices.GetService(typeof(LetsCheckIn.Helpers.IDynamicPermissionService));
            return await permissionService.HasAnyPermissionAsync(userId, permissions);
        }
    }
} 