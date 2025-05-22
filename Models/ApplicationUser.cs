using Microsoft.AspNetCore.Identity;

namespace AttendEase.Models
{
    public class ApplicationUser : IdentityUser
    {
        // You can add custom fields here if needed (e.g., FullName, BranchId, etc.)
    }

    public class ApplicationRole : IdentityRole
    {
    }
}
