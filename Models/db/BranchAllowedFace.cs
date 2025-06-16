using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetsCheckIn.Models.db
{
    public class BranchAllowedFace
    {
        [Key]
        public int FaceId { get; set; }
        public int SetupId { get; set; }
        public BranchSetup BranchSetup { get; set; } = null!;
        public string BiometricId { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public DateTime CreatedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
    }
}
