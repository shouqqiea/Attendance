using Microsoft.AspNetCore.Mvc.Rendering;
using LetsCheckIn.Models.db;

namespace LetsCheckIn.Models
{
    /// <summary>
    /// View model for displaying branch hierarchy information
    /// </summary>
    public class BranchHierarchyViewModel
    {
        /// <summary>
        /// Flat list of accessible branches for the current user
        /// </summary>
        public List<BranchHierarchyItem> AccessibleBranches { get; set; } = new List<BranchHierarchyItem>();

        /// <summary>
        /// Hierarchical tree structure of accessible branches
        /// </summary>
        public List<BranchHierarchyItem> BranchTree { get; set; } = new List<BranchHierarchyItem>();

        /// <summary>
        /// Select list items for dropdowns, properly formatted with hierarchy levels
        /// </summary>
        public List<SelectListItem> BranchSelectItems { get; set; } = new List<SelectListItem>();

        /// <summary>
        /// Select list items for potential parent branches (only Corporate branches)
        /// </summary>
        public List<SelectListItem> ParentBranchSelectItems { get; set; } = new List<SelectListItem>();
    }

    /// <summary>
    /// Represents a branch item in the hierarchy with metadata
    /// </summary>
    public class BranchHierarchyItem
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int BranchType { get; set; }
        public string BranchTypeName => BranchType == 1 ? "Corporate" : "Single";
        public int? ParentBranchId { get; set; }
        public string? ParentBranchName { get; set; }
        public bool IsSuperAdminBranch { get; set; }
        public int Level { get; set; } = 0; // Hierarchy level (0 = root, 1 = first level child, etc.)
        public int EmployeeCount { get; set; }
        public int ChildBranchCount { get; set; }
        public bool CanHaveChildren => BranchType == 1;
        public bool IsRootBranch => ParentBranchId == null;
        
        /// <summary>
        /// Child branches for hierarchical display
        /// </summary>
        public List<BranchHierarchyItem> Children { get; set; } = new List<BranchHierarchyItem>();

        /// <summary>
        /// Display name with hierarchy level indentation for dropdowns
        /// </summary>
        public string DisplayName => new string('-', Level * 2) + (Level > 0 ? " " : "") + BranchName;

        /// <summary>
        /// Full hierarchy path for display (e.g., "Main > Region A > Branch 1")
        /// </summary>
        public string HierarchyPath { get; set; } = string.Empty;
    }
} 