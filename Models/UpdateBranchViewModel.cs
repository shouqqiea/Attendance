using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models
{
    public class UpdateBranchViewModel
    {
        public int BranchId { get; set; }

        [Required(ErrorMessage = "Branch Name is required")]
        public string BranchName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Admin Name is required")]
        public string AdminName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Admin Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string AdminEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "At least Location must be enabled")]
        public bool EnableLocation { get; set; }
        
        public bool EnableNetwork { get; set; }
        public bool EnableBiometric { get; set; }
    }
} 