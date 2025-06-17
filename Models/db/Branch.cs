using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace LetsCheckIn.Models.db
{
    /// <summary>
    /// Represents a branch entity with hierarchical support for corporate and single branch types
    /// </summary>
    public class Branch
    {
        [Key]
        public int BranchId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string BranchName { get; set; } = string.Empty;

        [Required]
        public int AdminId { get; set; }

        /// <summary>
        /// Branch type: 1 = Corporate Branch (can have children), 2 = Single Branch (cannot have children)
        /// </summary>
        [Required]
        [Range(1, 2, ErrorMessage = "BranchType must be 1 (Corporate) or 2 (Single)")]
        public int BranchType { get; set; } = 2; // Default to Single Branch

        /// <summary>
        /// Foreign key to parent branch for hierarchical structure. Null for root branches.
        /// </summary>
        public int? ParentBranchId { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        public DateTime? DeletedDate { get; set; }

        [Required]
        public bool IsSuperAdminBranch { get; set; } = false;

        // Navigation Properties
        /// <summary>
        /// Reference to the admin who manages this branch
        /// </summary>
        public Admin? Admin { get; set; }

        /// <summary>
        /// Reference to the parent branch in the hierarchy
        /// </summary>
        [ForeignKey("ParentBranchId")]
        public Branch? ParentBranch { get; set; }

        /// <summary>
        /// Collection of child branches (only applicable for Corporate branches)
        /// </summary>
        public ICollection<Branch>? ChildBranches { get; set; }

        /// <summary>
        /// Collection of employees belonging to this branch
        /// </summary>
        public ICollection<Employee>? Employees { get; set; }

        /// <summary>
        /// Collection of attendance records for this branch
        /// </summary>
        public ICollection<AttendanceRecord>? AttendanceRecords { get; set; }

        /// <summary>
        /// Collection of holidays specific to this branch
        /// </summary>
        public ICollection<BranchHoliday>? BranchHolidays { get; set; }

        /// <summary>
        /// Setup configuration for this branch
        /// </summary>
        public BranchSetup? BranchSetup { get; set; }

        // Helper Properties
        /// <summary>
        /// Indicates whether this branch is a Corporate type that can have child branches
        /// </summary>
        [NotMapped]
        public bool IsCorporateBranch => BranchType == 1;

        /// <summary>
        /// Indicates whether this branch is a Single type that cannot have child branches
        /// </summary>
        [NotMapped]
        public bool IsSingleBranch => BranchType == 2;

        /// <summary>
        /// Indicates whether this branch is a root branch (has no parent)
        /// </summary>
        [NotMapped]
        public bool IsRootBranch => ParentBranchId == null;
    }
}
