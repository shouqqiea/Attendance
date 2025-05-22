using System.ComponentModel.DataAnnotations;

namespace AttendEase.Models.db
{
    public class LeaveRequest
    {
        [Key]
        public int LeaveRequestId { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public int LeaveTypeId { get; set; }
        public LeaveType LeaveType { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; }  // e.g., Pending, Approved, Rejected
        public string ManagerRemark { get; set; }
        public DateTime SubmissionDate { get; set; }
    }

}
