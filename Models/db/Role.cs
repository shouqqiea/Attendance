using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models.db
{
    public class Role
    {
        [Key]
        public int RoleId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string RoleName { get; set; }
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public bool IsSystemRole { get; set; } = false; // For built-in roles that cannot be deleted
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        public string? CreatedBy { get; set; }
        
        public DateTime? UpdatedDate { get; set; }
        
        public DateTime? DeletedDate { get; set; }
        
        // Navigation properties
        public virtual ICollection<BranchRole> BranchRoles { get; set; } = new HashSet<BranchRole>();
        public virtual ICollection<DynamicUserRole> UserRoles { get; set; } = new HashSet<DynamicUserRole>();
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new HashSet<RolePermission>();

        public Role()
        {
            RolePermissions = new HashSet<RolePermission>();
            UserRoles = new HashSet<DynamicUserRole>();
            BranchRoles = new HashSet<BranchRole>();
        }
    }
} 