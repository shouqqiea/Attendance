# Branch Deletion and Role Management Fix

## Issue Description
1. **Original Issue**: Deleted branches were still appearing in the Role Management page even though they were properly soft-deleted from the Branch Management interface.
2. **Critical Issue**: Users belonging to deleted branches could not login anymore because their accounts were orphaned (pointing to non-existent/deleted branches).

## Root Cause
1. When branches were deleted using the `DeleteBranch` method, only the branch itself and its setup were soft-deleted (by setting `DeletedDate`)
2. Related `BranchRole` mappings remained active (`IsActive = true`), causing deleted branch names to still appear in role assignments
3. The Role Management queries didn't filter out assignments to deleted branches
4. **Critical**: User accounts (`ApplicationUser.BranchId`) were not updated when their branch was deleted, causing login failures

## Solution Implemented

### 1. Enhanced Branch Deletion Process
**File**: `Controllers/AccountController.cs` - `DeleteBranch` method

Added comprehensive cleanup when deleting a branch:
- **Branch Role Cleanup**: Deactivate all `BranchRole` records for the deleted branch
- **Employee Cleanup**: Soft-delete all employee records belonging to the branch
- **🔥 User Account Transfer**: Transfer users to an active branch to prevent login issues
- **🔥 Role Deactivation**: Deactivate user roles for security
- **Leave Request Cleanup**: Cancel pending leave requests for affected employees
- **Validation**: Prevent deletion of the last remaining branch
- **Existing**: Branch and BranchSetup soft-deletion (already implemented)

```csharp
// Deactivate all role assignments for this branch
var branchRoles = await _db.BranchRoles
    .Where(br => br.BranchId == id && br.IsActive)
    .ToListAsync();

foreach (var branchRole in branchRoles)
{
    branchRole.IsActive = false;
}

// Also soft delete any employee records for this branch
var employees = await _db.Employee
    .Where(e => e.BranchId == id && e.DeletedDate == null)
    .ToListAsync();

foreach (var employee in employees)
{
    employee.DeletedDate = DateTime.UtcNow;
    employee.Status = false;

    if (employee.User != null)
    {
        employee.User.BranchId = transferToBranch.BranchId;
        
        var userRoles = await _db.DynamicUserRoles
            .Where(ur => ur.UserId == employee.UserId && ur.IsActive)
            .ToListAsync();
        
        foreach (var userRole in userRoles)
        {
            userRole.IsActive = false;
        }
    }
}
```

### 2. Enhanced Role Management Queries
**File**: `Controllers/RoleManagementController.cs`

Added filters to exclude deleted branches in all role-related queries:

- **Index method**: Filter branch assignments to exclude deleted branches
- **Details method**: Filter branch assignments in role details
- **GetAssignedBranches**: Already had proper filtering
- **GetUnassignedBranches**: Already had proper filtering

```csharp
// Before (showing deleted branches)
var branchAssignments = await _context.BranchRoles
    .Where(br => br.RoleId == role.RoleId && br.IsActive)
    .Include(br => br.Branch)
    .ToListAsync();

// After (filtering out deleted branches)
var branchAssignments = await _context.BranchRoles
    .Where(br => br.RoleId == role.RoleId && br.IsActive)
    .Include(br => br.Branch)
    .Where(br => br.Branch.DeletedDate == null) // Filter out deleted branches
    .ToListAsync();
```

### 3. Data Cleanup Scripts

#### **File**: `SQL/04_cleanup_orphaned_branch_roles.sql`
Cleans up orphaned BranchRole records:
- Identifies `BranchRole` records pointing to deleted branches
- Deactivates these orphaned records
- Provides verification of the cleanup

#### **🔥 File**: `SQL/05_fix_orphaned_users_in_deleted_branches.sql`
**Critical fix for existing orphaned users**:
- Identifies users belonging to deleted branches who can't login
- Transfers them to the first active branch
- Deactivates their roles for security
- Provides verification of the fix

## Impact
- ✅ **Fixed**: Deleted branches no longer appear in Role Management
- ✅ **Fixed**: Role assignments are properly cleaned up when branches are deleted
- ✅ **Critical Fix**: Users can now login even if their original branch was deleted
- ✅ **Security**: User roles are deactivated when branches are deleted (must be manually reassigned)
- ✅ **Data Integrity**: Leave requests are properly cancelled
- ✅ **Prevention**: Cannot delete the last remaining branch
- ✅ **Cleanup**: Scripts available to fix existing orphaned data

## Testing Steps
1. **For new deletions**:
   - Delete a branch from Branch Management
   - Verify users from that branch can still login (they'll be transferred to another branch)
   - Check Role Management - deleted branch should not appear in any role assignments
   - Verify user roles are deactivated (users will need roles reassigned)

2. **For existing orphaned users**:
   - Run the SQL cleanup scripts `04_cleanup_orphaned_branch_roles.sql` and `05_fix_orphaned_users_in_deleted_branches.sql`
   - Verify all users can login again
   - Reassign roles through the Role Management interface

## Related Files Modified
- `Controllers/AccountController.cs` - Enhanced DeleteBranch method with user transfer logic
- `Controllers/RoleManagementController.cs` - Enhanced queries to filter deleted branches
- `SQL/04_cleanup_orphaned_branch_roles.sql` - Branch role cleanup script
- `SQL/05_fix_orphaned_users_in_deleted_branches.sql` - **Critical user transfer script**
- `Documentation/BRANCH_DELETION_FIX.md` - This documentation

## 🚨 Immediate Action Required
If you have users who cannot login due to deleted branches, **run this SQL script immediately**:
```sql
-- Run SQL/05_fix_orphaned_users_in_deleted_branches.sql
```

This will transfer all orphaned users to active branches and allow them to login again. Their roles will be deactivated for security and must be reassigned through the Role Management interface.

## Prevention
This fix ensures that:
1. All related data is properly cleaned up when a branch is deleted
2. Users are never orphaned - they're always transferred to an active branch
3. UI queries consistently filter out deleted branches
4. Data integrity is maintained across the role management system
5. Security is preserved by deactivating roles when branches change 