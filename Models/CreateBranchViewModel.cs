using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LetsCheckIn.Models
{
    /// <summary>
    /// View model for creating new branches with hierarchy support
    /// </summary>
    public class CreateBranchViewModel
    {
        [Required(ErrorMessage = "Branch name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Branch name must be between 2 and 100 characters")]
        public string BranchName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Admin name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Admin name must be between 2 and 100 characters")]
        public string AdminName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Admin email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string AdminEmail { get; set; } = string.Empty;

        /// <summary>
        /// Branch type: 1 = Corporate Branch (can have children), 2 = Single Branch (cannot have children)
        /// </summary>
        [Required(ErrorMessage = "Branch type is required")]
        [Range(1, 2, ErrorMessage = "Branch type must be Corporate (1) or Single (2)")]
        public int BranchType { get; set; } = 2; // Default to Single Branch

        /// <summary>
        /// Parent branch ID for creating child branches. Null for root branches.
        /// </summary>
        public int? ParentBranchId { get; set; }

        [Required(ErrorMessage = "Location tracking must be enabled")]
        public bool EnableLocation { get; set; } = true;

        public bool EnableNetwork { get; set; }

        public bool EnableBiometric { get; set; }

        // Helper properties for UI display
        public string BranchTypeName => BranchType == 1 ? "Corporate Branch" : "Single Branch";

        /// <summary>
        /// List of available parent branches for dropdown selection
        /// </summary>
        public List<SelectListItem> AvailableParentBranches { get; set; } = new List<SelectListItem>();

        /// <summary>
        /// Validation method to ensure hierarchy rules are followed
        /// </summary>
        public bool IsValidHierarchy()
        {
            // Root branches can be any type
            if (ParentBranchId == null) return true;

            // Child branches require a parent branch to be selected
            return ParentBranchId.HasValue && ParentBranchId.Value > 0;
        }
    }
}
