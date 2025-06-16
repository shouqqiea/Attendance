using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetsCheckIn.Models.db
{
    public class BranchAllowedNetwork
    {
        [Key]
        public int NetworkId { get; set; }
        public int SetupId { get; set; }
        public BranchSetup BranchSetup { get; set; } = null!;
        public string IPAddress { get; set; } = string.Empty;
        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}
