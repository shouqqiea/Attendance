using System.ComponentModel.DataAnnotations;

namespace AttendEase.Models.db
{
    public class LeaveType
    {
        [Key]
        public int LeaveTypeId { get; set; }
        public string TypeName { get; set; }
        public int DefaultBalance { get; set; }

        public ICollection<LeaveRequest> LeaveRequests { get; set; }
    }

}
