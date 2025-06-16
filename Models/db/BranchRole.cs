using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models.db
{
    public class BranchRole
    {
        [Key]
        public int BranchRoleId { get; set; }
        
        public int BranchId { get; set; }
        
        public int RoleId { get; set; }
        
        public string AssignedBy { get; set; }
        
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual Branch Branch { get; set; }
        public virtual Role Role { get; set; }
    }
} 