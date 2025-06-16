using Microsoft.AspNetCore.Identity;

namespace LetsCheckIn.Models
{
    public class ApplicationRole : IdentityRole
    {
        public ApplicationRole() : base() { }
        
        public ApplicationRole(string roleName) : base(roleName) { }
    }
} 