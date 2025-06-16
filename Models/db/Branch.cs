using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace LetsCheckIn.Models.db
{
    public class Branch
    {
        [Key]
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int AdminId { get; set; }
        public Admin? Admin { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool IsSuperAdminBranch { get; set; } = false;
        public ICollection<Employee>? Employees { get; set; }
        public ICollection<AttendanceRecord>? AttendanceRecords { get; set; }
        public ICollection<BranchHoliday>? BranchHolidays { get; set; }
        public BranchSetup? BranchSetup { get; set; }
    }

}
