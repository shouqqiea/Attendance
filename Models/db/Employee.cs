using System.ComponentModel.DataAnnotations;

namespace AttendEase.Models.db
{
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool Status { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; }

        public ICollection<AttendanceRecord> AttendanceRecords { get; set; }
        public ICollection<LeaveRequest> LeaveRequests { get; set; }
    }

}
