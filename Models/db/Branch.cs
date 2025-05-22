using System.ComponentModel.DataAnnotations;

namespace AttendEase.Models.db
{
    public class Branch
    {
        [Key]
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string LocationCoordinates { get; set; }
        public string AllowedIPAddresses { get; set; }

        public int AdminId { get; set; }
        public Admin Admin { get; set; }

        public ICollection<Employee> Employees { get; set; }
        public ICollection<AttendanceRecord> AttendanceRecords { get; set; }
        public ICollection<BranchHoliday> BranchHolidays { get; set; }
    }

}
