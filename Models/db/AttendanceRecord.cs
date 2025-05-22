using System.ComponentModel.DataAnnotations;

namespace AttendEase.Models.db
{
    public class AttendanceRecord
    {
        [Key]
        public int AttendanceId { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; }

        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string Note { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string IPAddress { get; set; }
        public bool IsValidCheckIn { get; set; }
    }

}
