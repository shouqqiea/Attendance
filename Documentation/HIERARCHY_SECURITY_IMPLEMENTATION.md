# Branch Hierarchy Security and Filtering Implementation

## Overview
This document outlines the comprehensive security and filtering layer implementation for the .NET Core MVC branch hierarchy system. The implementation ensures that all data access respects branch hierarchy rules while maintaining performance and security.

## Key Components

### 1. Security Audit Logging (`SecurityAuditLog`)
- **Entity**: `Models/db/SecurityAuditLog.cs`
- **Purpose**: Tracks all security-related events for compliance and monitoring
- **Features**:
  - User activity tracking
  - Security violation detection
  - Cross-branch access monitoring
  - Data export auditing
  - Failed access attempt logging

**Key Fields**:
- `UserId`: User performing the action
- `Action`: Type of action (e.g., BRANCH_ACCESS_ATTEMPT, DATA_EXPORT)
- `ResourceType`: Type of resource accessed (Branch, Employee, LeaveRequest)
- `ResourceId`: ID of the specific resource
- `IsSecurityViolation`: Flag for security violations
- `IPAddress`, `UserAgent`, `RequestPath`: Request context
- `Timestamp`: When the action occurred

### 2. Hierarchy Security Service (`HierarchySecurityService`)
- **Location**: `Helpers/HierarchySecurityService.cs`
- **Purpose**: Provides comprehensive security validation and audit logging
- **Capabilities**:

#### Core Security Validation
```csharp
Task<bool> ValidateBranchAccessAsync(string userId, int branchId);
Task<bool> ValidateBranchSelectionAsync(string userId, int[] branchIds);
Task<bool> ValidateDataExportPermissionAsync(string userId, int branchId);
Task<bool> ValidateRoleAssignmentPermissionAsync(string userId, int roleId, int targetBranchId);
```

#### Data Filtering Helpers
```csharp
Task<IQueryable<Employee>> FilterEmployeesByAccessibleBranchesAsync(IQueryable<Employee> query, string userId);
Task<IQueryable<LeaveRequest>> FilterLeaveRequestsByAccessibleBranchesAsync(IQueryable<LeaveRequest> query, string userId);
Task<IQueryable<Branch>> FilterBranchesByAccessibilityAsync(IQueryable<Branch> query, string userId);
```

#### Audit and Logging
```csharp
Task LogSecurityEventAsync(string userId, string action, string resourceType, int resourceId, string details = "", bool isViolation = false);
Task LogCrossBranchAccessAttemptAsync(string userId, int sourceBranchId, int targetBranchId, bool success);
Task LogDataExportEventAsync(string userId, string exportType, int[] branchIds, int recordCount);
```

### 3. Authorization Attributes
- **Location**: `Helpers/BranchHierarchyAuthorizationAttribute.cs`
- **Components**:

#### `BranchHierarchyAuthorizeAttribute`
- Validates basic permissions and branch access
- Supports configurable branch ID parameter extraction
- Logs successful and failed authorization attempts

#### `CrossBranchAuthorizeAttribute`
- Validates operations across multiple branches
- Ensures users can access both source and target branches
- Suitable for transfer operations

#### `BulkBranchAuthorizeAttribute`
- Validates operations on multiple branches simultaneously
- Supports comma-separated branch ID lists
- Ideal for bulk export/report operations

### 4. Enhanced BranchAccessService
- **Location**: `Models/BranchAccessService.cs`
- **Enhancements**:
  - Hierarchy-aware access validation
  - Descendant branch resolution for Corporate branches
  - Root branch detection
  - Child branch creation validation

**Key Methods**:
```csharp
Task<List<int>> GetDescendantBranchIdsAsync(int branchId); // Recursive descendant lookup
Task<bool> CanCreateChildBranchAsync(string userId, int? parentBranchId); // Child creation validation
Task<bool> IsValidBranchHierarchyAsync(int? parentBranchId, int childBranchType); // Hierarchy validation
```

## Security Implementation Patterns

### 1. Hierarchy Access Rules
- **SuperAdmin**: Access to all branches globally
- **Corporate Branch Users**: Access to own branch + all descendant branches
- **Single Branch Users**: Access only to own branch
- **Cross-Branch Operations**: Both source and target must be accessible

### 2. Data Filtering Strategy
**Before (Basic)**:
```csharp
var query = _context.LeaveRequests.Where(lr => accessibleBranchIds.Contains(lr.Employee.BranchId));
```

**After (Hierarchy-Aware)**:
```csharp
var query = _context.LeaveRequests.Include(lr => lr.Employee);
var filteredQuery = await _hierarchySecurityService.FilterLeaveRequestsByAccessibleBranchesAsync(query, userId);
```

### 3. Audit Logging Pattern
```csharp
// Log every significant operation
await _hierarchySecurityService.LogSecurityEventAsync(userId, "LEAVE_APPROVAL_ACCESS", "Controller", 0,
    "Accessing leave approval dashboard");

// Log security violations
await _hierarchySecurityService.LogSecurityEventAsync(userId, "INVALID_BRANCH_SELECTION", "BranchSelection", 0,
    $"Attempted to access unauthorized branches: {string.Join(", ", invalidBranches)}", true);
```

## Database Optimizations

### Performance Indexes
The SQL script `SQL/02_update_security_indexes.sql` creates:

1. **Branch Hierarchy Indexes**:
   - `IX_Branch_ParentBranchId_Hierarchy`: Fast parent-child traversal
   - `IX_Branch_Type_Deleted_Composite`: Branch type filtering

2. **Employee Access Indexes**:
   - `IX_Employee_BranchId_Enhanced`: Employee-branch lookups
   - `IX_Employee_UserId_Branch`: User-employee resolution

3. **Security Audit Indexes**:
   - `IX_SecurityAuditLog_User_Time`: User activity tracking
   - `IX_SecurityAuditLog_Violations`: Security violation detection
   - `IX_SecurityAuditLog_Action_Resource`: Action-based queries

4. **Leave Request Indexes**:
   - `IX_LeaveRequests_Employee_Branch`: Hierarchy-aware filtering
   - `IX_LeaveRequests_Status_Date`: Status and date filtering

### Stored Procedures
- `sp_GetChildBranches`: Recursive child branch lookup with CTE
- `sp_GetUserAccessibleBranches`: Optimized user access resolution

### Security Views
- `vw_UserBranchAccess`: Efficient user branch access checking

## Controller Updates

### LeaveManagementController Enhancement
**Key Changes**:
1. Injected `IHierarchySecurityService`
2. Replaced basic branch filtering with hierarchy-aware methods
3. Added comprehensive audit logging
4. Enhanced security validation for all operations

**Example Update**:
```csharp
// Old approach
var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(user.Id);
var query = _context.LeaveRequests.Where(lr => accessibleBranches.Contains(lr.Employee.BranchId));

// New approach
await _hierarchySecurityService.LogSecurityEventAsync(user.Id, "LEAVE_APPROVAL_ACCESS", "Controller", 0,
    "Accessing leave approval dashboard");

var leaveRequestQuery = _context.LeaveRequests.Include(lr => lr.Employee);
var filteredQuery = await _hierarchySecurityService.FilterLeaveRequestsByAccessibleBranchesAsync(leaveRequestQuery, user.Id);
```

## Security Features

### 1. Access Pattern Monitoring
- Detects excessive cross-branch access (>50 in 24 hours)
- Monitors failed access attempts (>10 in 1 hour)
- Flags suspicious high-frequency access patterns

### 2. Violation Detection
```csharp
var violations = await _hierarchySecurityService.DetectSecurityViolationsAsync();
// Returns list of SecurityViolation objects with severity levels
```

### 3. Hierarchy Consistency Validation
```csharp
var validationResult = await _hierarchySecurityService.ValidateHierarchyConsistencyAsync();
// Checks for circular references, orphaned branches, invalid hierarchies
```

### 4. Cross-Branch Operation Security
```csharp
var canPerform = await _hierarchySecurityService.CanPerformCrossBranchOperationAsync(userId, sourceBranchId, targetBranchId);
// Validates both source and target branch access
```

## Usage Examples

### 1. Securing Controller Actions
```csharp
[BranchHierarchyAuthorize("leave.approve", "branchId")]
public async Task<IActionResult> ApproveLeave(int branchId, int leaveRequestId)
{
    // Action is automatically secured by attribute
    // Branch access is validated before method execution
}
```

### 2. Bulk Operations
```csharp
[BulkBranchAuthorize("data.export", "branchIds")]
public async Task<IActionResult> ExportData(string branchIds)
{
    // Validates access to all specified branches
}
```

### 3. Data Filtering in Services
```csharp
public async Task<List<Employee>> GetEmployeesAsync(string userId)
{
    var query = _context.Employee.Include(e => e.Branch);
    var filteredQuery = await _hierarchySecurityService.FilterEmployeesByAccessibleBranchesAsync(query, userId);
    return await filteredQuery.ToListAsync();
}
```

## Migration and Deployment

### 1. Database Changes
Execute SQL scripts in order:
1. `Migrations/20250617114011_AddBranchHierarchy.cs` (Entity Framework migration)
2. `SQL/01_add_branch_hierarchy.sql` (Initial hierarchy setup)
3. `SQL/02_update_security_indexes.sql` (Performance and security optimization)

### 2. Service Registration
Services are registered in `Program.cs`:
```csharp
builder.Services.AddScoped<IHierarchySecurityService, HierarchySecurityService>();
builder.Services.AddHttpContextAccessor();
```

## Security Considerations

### 1. Performance Impact
- Audit logging is asynchronous where possible
- Indexes optimize common query patterns
- Caching strategies for frequently accessed hierarchy data

### 2. Data Privacy
- Sensitive information is not logged in audit details
- IP addresses and user agents are captured for security analysis
- Audit logs respect data retention policies

### 3. Error Handling
- Graceful degradation when audit logging fails
- Security violations don't break core functionality
- Comprehensive error logging for debugging

## Monitoring and Maintenance

### 1. Security Metrics
Monitor the `SecurityAuditLogs` table for:
- Failed access attempts by user
- Cross-branch access patterns
- Security violations by type and severity
- Data export activity

### 2. Performance Monitoring
Track query performance on:
- Branch hierarchy traversal operations
- Employee filtering by accessible branches
- Leave request filtering operations
- Security audit log queries

### 3. Regular Maintenance
- Archive old audit logs (recommend 2-year retention)
- Update statistics on indexed tables monthly
- Review and optimize query plans quarterly
- Validate hierarchy consistency weekly

## Future Enhancements

### 1. Advanced Security Features
- Machine learning-based anomaly detection
- Geolocation-based access validation
- Time-based access restrictions
- Integration with SIEM systems

### 2. Performance Optimizations
- Redis caching for frequently accessed hierarchy data
- Background processing for non-critical audit events
- Materialized views for complex hierarchy queries

### 3. Compliance Features
- GDPR compliance for audit data
- SOX compliance reporting
- Custom compliance rule engines
- Automated compliance monitoring

## Conclusion

This implementation provides a comprehensive, secure, and performant solution for branch hierarchy management. It ensures that:

1. **Security**: All data access respects hierarchy rules with comprehensive audit logging
2. **Performance**: Optimized indexes and queries maintain fast response times
3. **Compliance**: Detailed audit trails support regulatory requirements
4. **Maintainability**: Clear separation of concerns and extensible architecture
5. **Monitoring**: Built-in security violation detection and performance monitoring

The system is production-ready and provides a solid foundation for enterprise-grade branch hierarchy management. 