using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LetsCheckIn.Models
{
    /// <summary>
    /// View model for updating existing branches with hierarchy support
    /// </summary>
    public class UpdateBranchViewModel
    {
        public int BranchId { get; set; }

        [Required(ErrorMessage = "Branch Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Branch name must be between 2 and 100 characters")]
        public string BranchName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Admin Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Admin name must be between 2 and 100 characters")]
        public string AdminName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Admin Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string AdminEmail { get; set; } = string.Empty;

        /// <summary>
        /// Branch type: 1 = Corporate Branch (can have children), 2 = Single Branch (cannot have children)
        /// Read-only for existing branches to maintain hierarchy integrity
        /// </summary>
        public int BranchType { get; set; }

        /// <summary>
        /// Parent branch ID. Read-only for existing branches to maintain hierarchy integrity
        /// </summary>
        public int? ParentBranchId { get; set; }

        /// <summary>
        /// Parent branch name for display purposes
        /// </summary>
        public string? ParentBranchName { get; set; }

        [Required(ErrorMessage = "At least Location must be enabled")]
        public bool EnableLocation { get; set; }
        
        public bool EnableNetwork { get; set; }
        
        public bool EnableBiometric { get; set; }

        // Helper properties for UI display
        public string BranchTypeName => BranchType == 1 ? "Corporate Branch" : "Single Branch";

        /// <summary>
        /// Indicates if this is a root branch (has no parent)
        /// </summary>
        public bool IsRootBranch => ParentBranchId == null;

        /// <summary>
        /// Indicates if this branch can have child branches
        /// </summary>
        public bool CanHaveChildren => BranchType == 1;

        /// <summary>
        /// List of current child branches for display
        /// </summary>
        public List<ChildBranchInfo> ChildBranches { get; set; } = new List<ChildBranchInfo>();

        /// <summary>
        /// Information about child branches
        /// </summary>
        public class ChildBranchInfo
        {
            public int BranchId { get; set; }
            public string BranchName { get; set; } = string.Empty;
            public string BranchTypeName { get; set; } = string.Empty;
            public int EmployeeCount { get; set; }
        }
    }
} 