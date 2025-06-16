using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models.db
{
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string PermissionName { get; set; }
        
        [StringLength(255)]
        public string Description { get; set; }
        
        [StringLength(50)]
        public string Category { get; set; } // e.g., "User Management", "Leave Management", "Branch Management"
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
} 