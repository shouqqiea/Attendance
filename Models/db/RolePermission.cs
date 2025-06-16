using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models.db
{
    public class RolePermission
    {
        [Key]
        public int RolePermissionId { get; set; }
        
        [Required]
        public int RoleId { get; set; }
        
        [Required]
        public int PermissionId { get; set; }
        
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
        
        public string? AssignedBy { get; set; } // UserId of who assigned the permission
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual Role Role { get; set; }
        public virtual Permission Permission { get; set; }
    }
} 