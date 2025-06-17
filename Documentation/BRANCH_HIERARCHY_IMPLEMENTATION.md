# Branch Hierarchy Access Control Implementation

## Overview

This document outlines the implementation of hierarchical branch access control for the LetsCheckIn system. The system now supports two branch types with different access rules:

- **Corporate Branch (Type 1)**: Can have child branches and users can access descendant branches
- **Single Branch (Type 2)**: Cannot have child branches, users can only access their own branch

## New Database Structure

### Branch Model Updates
- `BranchType`: 1 = Corporate, 2 = Single
- `ParentBranchId`: Foreign key to parent branch (null for root branches)
- Helper properties: `IsCorporateBranch`, `IsSingleBranch`, `IsRootBranch`

## Access Control Rules

### SuperAdmin (Main Branch)
- Can access **all branches** regardless of type
- Can create root branches and child branches
- Can assign any role except SuperAdmin to non-SuperAdmin branches
- SuperAdmin role can only be assigned to users on the SuperAdmin branch

### Corporate Branch Users
- Can access **own branch + all descendant branches** (recursive)
- Admin on Corporate branch can create child branches
- Can assign roles to users in accessible branches

### Single Branch Users
- Can **only access their own branch**
- Admin on Single branch cannot create child branches
- Can only manage users within their own branch

## Updated Services

### IBranchAccessService - New Methods

```csharp
// Hierarchy navigation
Task<List<int>> GetChildBranchIdsAsync(int branchId);
Task<List<int>> GetDescendantBranchIdsAsync(int branchId);
Task<List<BranchHierarchyNode>> GetBranchHierarchyAsync(int rootBranchId);
Task<Branch?> GetRootBranchAsync(int branchId);

// Access control
Task<bool> CanCreateChildBranchAsync(string userId, int? parentBranchId);
Task<bool> IsValidBranchHierarchyAsync(int? parentBranchId, int childBranchType);
```

### IDynamicPermissionService - New Methods

```csharp
// Branch-aware permissions
Task<bool> HasPermissionForBranchAsync(string userId, string permissionName, int branchId);
Task<List<Role>> GetAccessibleBranchRolesAsync(string userId);
Task<List<string>> GetUserPermissionsAcrossHierarchyAsync(string userId);
Task<bool> CanAssignRoleToBranchAsync(string userId, int roleId, int targetBranchId);
```

## Implementation Details

### BranchAccessService Updates

#### CanAccessBranchAsync()
- **Enhanced Logic**: Now checks branch hierarchy
- Corporate branch users can access descendant branches
- Single branch users restricted to own branch only
- SuperAdmin maintains full access

#### GetAccessibleBranchIdsAsync()
- **Returns**: User's own branch + all descendant branches (for Corporate)
- **Corporate Branch**: Includes all children, grandchildren, etc.
- **Single Branch**: Only returns own branch ID

#### CanCreateBranchAsync()
- **Updated Rule**: Admin can only create branches if on Corporate branch
- Single branch Admins cannot create child branches
- SuperAdmin can create any branch type

### New Hierarchy Methods

#### GetDescendantBranchIdsAsync()
```csharp
// Recursively finds all descendant branches
var descendants = new List<int>();
var childIds = await GetChildBranchIdsAsync(branchId);
descendants.AddRange(childIds);

foreach (var childId in childIds)
{
    var grandchildren = await GetDescendantBranchIdsAsync(childId);
    descendants.AddRange(grandchildren);
}
```

#### GetBranchHierarchyAsync()
```csharp
// Returns hierarchical tree structure
public class BranchHierarchyNode
{
    public Branch Branch { get; set; }
    public List<BranchHierarchyNode> Children { get; set; }
}
```

### DynamicPermissionService Updates

#### HasPermissionForBranchAsync()
- Checks if user has permission AND can access the specific branch
- Combines permission validation with hierarchy access rules

#### GetAccessibleBranchRolesAsync()
- Returns roles available across all accessible branches
- Useful for role assignment interfaces

## Usage Examples

### Checking Branch Access
```csharp
// Check if user can access a specific branch
var canAccess = await _branchAccessService.CanAccessBranchAsync(userId, branchId);

// Get all accessible branch IDs for a user
var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(userId);
```

### Managing Branch Hierarchy
```csharp
// Check if user can create a child branch
var canCreateChild = await _branchAccessService.CanCreateChildBranchAsync(userId, parentBranchId);

// Get all descendant branches
var descendants = await _branchAccessService.GetDescendantBranchIdsAsync(branchId);

// Get branch hierarchy tree
var hierarchy = await _branchAccessService.GetBranchHierarchyAsync(rootBranchId);
```

### Permission Checks
```csharp
// Check permission for specific branch
var hasPermission = await _permissionService.HasPermissionForBranchAsync(userId, "ViewLeaveData", branchId);

// Get permissions across all accessible branches
var allPermissions = await _permissionService.GetUserPermissionsAcrossHierarchyAsync(userId);
```

### Role Management
```csharp
// Check if role can be assigned to branch
var canAssignRole = await _permissionService.CanAssignRoleToBranchAsync(userId, roleId, targetBranchId);

// Get accessible roles across hierarchy
var accessibleRoles = await _permissionService.GetAccessibleBranchRolesAsync(userId);
```

## Validation Rules

### Branch Creation
1. Only Corporate branches can have children
2. SuperAdmin can create root branches
3. Admin on Corporate branch can create child branches
4. Admin on Single branch cannot create children

### Role Assignment
1. SuperAdmin can assign any role (except SuperAdmin to non-SuperAdmin branches)
2. Admin can assign Employee/Manager roles to accessible branches
3. System roles have additional validation

### Access Control
1. Users can only access branches within their hierarchy scope
2. Corporate branch access includes all descendants
3. Single branch access is limited to own branch
4. SuperAdmin has universal access

## Migration Considerations

### Existing Data
- Existing branches default to Single type (BranchType = 2)
- SuperAdmin branch remains unchanged
- No impact on existing user assignments

### Backward Compatibility
- All existing methods maintain functionality
- New methods are additive, not breaking changes
- Default behavior preserves current access patterns

## Security Enhancements

### Hierarchy Validation
- Prevents creation of invalid branch structures
- Validates parent-child relationships
- Ensures Corporate branches don't become children of Single branches

### Access Control
- Enforces strict hierarchy boundaries
- Prevents privilege escalation across branch types
- Maintains role assignment restrictions

### Audit Trail
- All hierarchy changes can be tracked
- Role assignments maintain branch context
- Permission checks include branch validation

## Testing Scenarios

### Corporate Branch Tests
1. User can access own branch and all descendants
2. Admin can create child branches
3. Role assignment works across accessible branches

### Single Branch Tests
1. User cannot access other branches
2. Admin cannot create child branches
3. Role assignment limited to own branch

### SuperAdmin Tests
1. Can access all branches regardless of type
2. Can create any branch type anywhere
3. Can assign roles to any branch

### Edge Cases
1. Circular hierarchy prevention
2. Orphaned branch handling
3. Branch type conversion scenarios 