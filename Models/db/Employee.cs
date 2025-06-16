using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetsCheckIn.Models.db
{
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;  // Foreign key to AspNetUsers table

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        public int BranchId { get; set; }  // Foreign key to Branch table

        [ForeignKey("BranchId")]
        public virtual Branch Branch { get; set; } = null!;

        public bool Status { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? DeletedDate { get; set; }

        // Navigation properties
        public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    }
}
