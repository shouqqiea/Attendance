using Microsoft.AspNetCore.Identity;
using LetsCheckIn.Models.db;

namespace LetsCheckIn.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? BranchId { get; set; }
        public DateTime? LastLoginDate { get; set; }

        // Navigation properties
        public virtual Employee? Employee { get; set; }
        public virtual ICollection<DynamicUserRole> UserRoles { get; set; } = new HashSet<DynamicUserRole>();
    }
}
