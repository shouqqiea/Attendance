using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetsCheckIn.Models.db
{
    public class StatusType
    {
        [Key]
        public int StatusId { get; set; }

        [Required]
        [StringLength(50)]
        public string StatusName { get; set; }

        // Navigation property for leave requests
        public virtual ICollection<LeaveRequest> LeaveRequests { get; set; }
    }
} 