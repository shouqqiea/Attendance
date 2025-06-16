using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetsCheckIn.Models.db
{
    public class LeaveType
    {
        [Key]
        public int LeaveTypeId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        
        [StringLength(255)]
        public string? Description { get; set; }
        
        public int DefaultDays { get; set; }
        
        public bool IsActive { get; set; } = true;

        [Required]
        public int BranchId { get; set; }

        [ForeignKey("BranchId")]
        public virtual Branch Branch { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? DeletedDate { get; set; }
        
        // Navigation property
        public virtual ICollection<LeaveRequest> LeaveRequests { get; set; }
    }
}
