using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LetsCheckIn.Models.db
{
    public class LeaveRequest
    {
        [Key]
        public int LeaveRequestId { get; set; }

        [Required]
        public int EmployeeId { get; set; }
        
        [Required]
        public int LeaveTypeId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public int StatusId { get; set; }

        [StringLength(500)]
        public string? LeaveReason { get; set; }

        [StringLength(500)]
        public string? RejectedReason { get; set; }

        [Required]
        public DateTime SubmissionDate { get; set; }

        public string? AttachmentPath { get; set; }

        // Navigation properties
        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        [ForeignKey("LeaveTypeId")]
        public virtual LeaveType LeaveType { get; set; }

        [ForeignKey("StatusId")]
        public virtual StatusType Status { get; set; }
    }
}
