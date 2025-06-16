using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetsCheckIn.Models.db
{
    public class DynamicUserRole
    {
        [Key]
        public int UserRoleId { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public int RoleId { get; set; }

        public bool IsActive { get; set; }
        public DateTime AssignedDate { get; set; }
        public string? AssignedBy { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }
    }
} 