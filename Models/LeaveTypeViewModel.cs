using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models
{
    public class LeaveTypeViewModel
    {
        public int? LeaveTypeId { get; set; }

        [Required(ErrorMessage = "Leave type name is required")]
        [StringLength(50, ErrorMessage = "Name cannot be longer than 50 characters")]
        public string Name { get; set; }

        [StringLength(255, ErrorMessage = "Description cannot be longer than 255 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Default days is required")]
        [Range(1, 365, ErrorMessage = "Default days must be between 1 and 365")]
        public int? DefaultDays { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        public int BranchId { get; set; }
    }
} 