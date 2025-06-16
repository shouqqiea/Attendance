using System.ComponentModel.DataAnnotations;

namespace LetsCheckIn.Models
{
    public class CreateBranchViewModel
    {
        [Required]
        public string BranchName { get; set; } = string.Empty;
        [Required]
        public string AdminName { get; set; } = string.Empty;
        [Required, EmailAddress]
        public string AdminEmail { get; set; } = string.Empty;
        [Required]
        public bool EnableLocation { get; set; }
        public bool EnableNetwork { get; set; }
        public bool EnableBiometric { get; set; }
    }
}
