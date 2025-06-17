# Backend Branch Hierarchy System Documentation

## Overview

This document provides a comprehensive guide to the branch hierarchy system implementation in the LetsCheckIn application. The system supports multi-level branch organization with Corporate and Single branch types, enabling scalable organizational structures.

## Table of Contents

1. [Database Schema](#database-schema)
2. [Core Models](#core-models)
3. [Access Control Services](#access-control-services)
4. [Controller Logic](#controller-logic)
5. [Security Implementation](#security-implementation)
6. [Performance Considerations](#performance-considerations)
7. [API Endpoints](#api-endpoints)
8. [Development Guidelines](#development-guidelines)

## Database Schema

### Branch Table Structure

```sql
CREATE TABLE [Branch] (
    [BranchId] int IDENTITY(1,1) PRIMARY KEY,
    [BranchName] nvarchar(100) NOT NULL,
    [AdminId] int NOT NULL,
    [BranchType] int NOT NULL DEFAULT 2,           -- 1 = Corporate, 2 = Single
    [ParentBranchId] int NULL,                     -- FK to parent branch
    [CreatedDate] datetime2 NOT NULL,
    [DeletedDate] datetime2 NULL,
    [IsSuperAdminBranch] bit NOT NULL DEFAULT 0,
    
    CONSTRAINT FK_Branch_Branch_ParentBranchId 
        FOREIGN KEY ([ParentBranchId]) REFERENCES [Branch]([BranchId]),
    CONSTRAINT CK_Branch_BranchType CHECK ([BranchType] IN (1, 2))
)
```

### Key Indexes
- `IX_Branch_BranchType` - Performance optimization for type-based queries
- `IX_Branch_ParentBranchId` - Hierarchy traversal optimization

### Branch Type Definitions
- **Type 1 (Corporate)**: Can have child branches, manage hierarchical operations
- **Type 2 (Single)**: Standalone branch, cannot have children
- **SuperAdmin Branch**: Special Corporate branch with global access (Main Branch)

## Core Models

### Branch Entity (`Models/db/Branch.cs`)

```csharp
public class Branch
{
    [Key]
    public int BranchId { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string BranchName { get; set; } = string.Empty;
    
    [Required]
    [Range(1, 2, ErrorMessage = "BranchType must be 1 (Corporate) or 2 (Single)")]
    public int BranchType { get; set; } = 2;
    
    public int? ParentBranchId { get; set; }
    
    // Navigation Properties
    [ForeignKey("ParentBranchId")]
    public Branch? ParentBranch { get; set; }
    public ICollection<Branch>? ChildBranches { get; set; }
    
    // Helper Properties
    [NotMapped]
    public bool IsCorporateBranch => BranchType == 1;
    
    [NotMapped]
    public bool IsSingleBranch => BranchType == 2;
    
    [NotMapped]
    public bool IsRootBranch => ParentBranchId == null;
}
```

### Key Features
- **Type Safety**: Enum-like constants with validation
- **Navigation Properties**: EF Core relationships for hierarchy
- **Helper Properties**: Computed properties for business logic

## Access Control Services

### IBranchAccessService Interface

Core service interface providing hierarchy-aware access control:

```csharp
public interface IBranchAccessService
{
    // Basic Access Control
    Task<bool> CanAccessBranchAsync(string userId, int branchId);
    Task<bool> IsSuperAdminAsync(string userId);
    Task<List<int>> GetAccessibleBranchIdsAsync(string userId);
    
    // Hierarchy Operations
    Task<List<int>> GetChildBranchIdsAsync(int branchId);
    Task<List<int>> GetDescendantBranchIdsAsync(int branchId);
    Task<List<BranchHierarchyNode>> GetBranchHierarchyAsync(int rootBranchId);
    
    // Branch Creation & Validation
    Task<bool> CanCreateChildBranchAsync(string userId, int? parentBranchId);
    Task<bool> IsValidBranchHierarchyAsync(int? parentBranchId, int childBranchType);
}
```

### Access Control Logic

#### Permission Matrix

| User Role | Branch Type | Access Scope |
|-----------|-------------|--------------|
| SuperAdmin | Main Branch | All branches globally |
| Admin | Corporate | Own branch + all descendant branches |
| Admin | Single | Own branch only |
| Employee/Manager | Any | Own branch only |

#### Hierarchy Traversal Algorithms

**Descendant Branch Retrieval:**
```csharp
public async Task<List<int>> GetDescendantBranchIdsAsync(int branchId)
{
    var result = new List<int>();
    await GetDescendantsRecursive(branchId, result);
    return result;
}

private async Task GetDescendantsRecursive(int parentId, List<int> result)
{
    var children = await _context.Branch
        .Where(b => b.ParentBranchId == parentId && b.DeletedDate == null)
        .Select(b => b.BranchId)
        .ToListAsync();
    
    result.AddRange(children);
    
    foreach (var childId in children)
    {
        await GetDescendantsRecursive(childId, result);
    }
}
```

## Controller Logic

### Branch Creation (`AccountController.CreateBranch`)

**Validation Flow:**
1. **Permission Check**: Verify user can create branches
2. **Hierarchy Validation**: Ensure parent-child relationship is valid
3. **Type Validation**: Corporate branches can have children, Single cannot
4. **Root Branch Restriction**: Only SuperAdmin can create root branches

**Key Implementation:**
```csharp
[HttpPost]
public async Task<IActionResult> CreateBranch(CreateBranchViewModel model)
{
    // Validate hierarchy rules
    if (model.ParentBranchId.HasValue)
    {
        if (!await _branchAccessService.CanCreateChildBranchAsync(currentUser.Id, model.ParentBranchId.Value))
        {
            ModelState.AddModelError("ParentBranchId", "Permission denied for child branch creation");
        }
    }
    else if (!await _branchAccessService.IsSuperAdminAsync(currentUser.Id))
    {
        ModelState.AddModelError("ParentBranchId", "Only SuperAdmin can create root branches");
    }
    
    // Create branch with hierarchy support
    var branch = new Branch
    {
        BranchName = model.BranchName,
        BranchType = model.BranchType,
        ParentBranchId = model.ParentBranchId,
        // ... other properties
    };
}
```

### Data Filtering (`LeaveManagementController`)

**Hierarchy-Aware Filtering:**
```csharp
public async Task<IActionResult> LeaveData(/* filters */)
{
    // Get accessible branches based on hierarchy
    var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(user.Id);
    
    var query = _context.LeaveRequests
        .Include(lr => lr.Employee)
        .Where(lr => accessibleBranches.Contains(lr.Employee.BranchId));
    
    // Apply additional filters...
}
```

## Security Implementation

### Security Audit Logging

**SecurityAuditLog Entity:**
```csharp
public class SecurityAuditLog
{
    public string UserId { get; set; }
    public string Action { get; set; }
    public string ResourceType { get; set; }
    public int ResourceId { get; set; }
    public string Severity { get; set; }
    public bool IsSecurityViolation { get; set; }
    public DateTime Timestamp { get; set; }
    // ... additional audit fields
}
```

### Cross-Branch Operation Protection

**Key Security Measures:**
1. **Input Validation**: All branch IDs validated against user's accessible branches
2. **Hierarchy Enforcement**: Corporate branches can only access actual descendants
3. **Role-Based Restrictions**: Single branches blocked from cross-branch operations
4. **Audit Trail**: All branch access attempts logged

### SQL Injection Prevention
- **Parameterized Queries**: All database operations use EF Core LINQ
- **Input Sanitization**: Model validation with data annotations
- **Type Safety**: Strongly-typed models and enums

## Performance Considerations

### Database Optimization

**Indexing Strategy:**
```sql
-- Type-based filtering
CREATE INDEX IX_Branch_BranchType ON Branch (BranchType) 
INCLUDE (BranchId, BranchName, ParentBranchId)

-- Hierarchy traversal
CREATE INDEX IX_Branch_ParentBranchId ON Branch (ParentBranchId) 
INCLUDE (BranchId, BranchName, BranchType)

-- Audit log performance
CREATE INDEX IX_SecurityAuditLog_UserId ON SecurityAuditLog (UserId)
CREATE INDEX IX_SecurityAuditLog_Timestamp ON SecurityAuditLog (Timestamp)
```

### Caching Strategy
- **Branch Hierarchy**: Cache static hierarchy data for 15 minutes
- **User Permissions**: Cache accessible branch lists per user session
- **Branch Metadata**: Cache branch type and parent information

### Query Optimization
- **Eager Loading**: Include related entities in single queries
- **Projection**: Select only required columns for dropdown lists
- **Batch Operations**: Group related database operations

## API Endpoints

### Branch Management

| Endpoint | Method | Purpose | Access Level |
|----------|--------|---------|--------------|
| `/Account/CreateBranch` | GET/POST | Create new branch | Admin+ |
| `/Account/BranchList` | GET | View accessible branches | All |
| `/Account/UpdateBranch/{id}` | GET/POST | Update branch details | Admin+ |
| `/Account/GetAvailableParentBranches` | GET | Get parent options | Admin+ |
| `/Account/GetBranchHierarchy` | GET | Get hierarchy tree | All |

### Data Filtering

| Endpoint | Method | Purpose | Hierarchy Support |
|----------|--------|---------|------------------|
| `/LeaveManagement/LeaveData` | GET | View leave data | ✅ Respects hierarchy |
| `/LeaveManagement/LeaveApproval` | GET | Approve leaves | ✅ Descendant branches |
| `/LeaveManagement/ExportToExcel` | GET | Export data | ✅ Filtered by access |

## Development Guidelines

### Adding New Hierarchy-Aware Features

1. **Always Use BranchAccessService**: Never query branches directly
2. **Validate Access**: Check `CanAccessBranchAsync` before operations
3. **Filter Data**: Use `GetAccessibleBranchIdsAsync` for data queries
4. **Log Operations**: Add audit entries for cross-branch operations

### Code Patterns

**Correct Pattern:**
```csharp
// ✅ Good: Use access service
var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(userId);
var data = await _context.SomeEntity
    .Where(e => accessibleBranches.Contains(e.BranchId))
    .ToListAsync();
```

**Incorrect Pattern:**
```csharp
// ❌ Bad: Direct branch access
var data = await _context.SomeEntity
    .Where(e => e.BranchId == userBranchId) // Misses child branches!
    .ToListAsync();
```

### Testing Considerations

**Unit Test Coverage:**
- Branch creation validation
- Hierarchy traversal algorithms  
- Access control logic
- Security boundary enforcement

**Integration Test Scenarios:**
- Multi-level hierarchy operations
- Cross-branch data access attempts
- Permission escalation prevention
- Audit logging verification

### Migration and Deployment

**Database Migration Steps:**
1. Run `SQL/01_add_branch_hierarchy.sql`
2. Run `SQL/02_update_security_indexes.sql`
3. Verify branch type assignments
4. Test access control functionality

**Rollback Considerations:**
- Preserve existing data structure
- Maintain backward compatibility
- Gradual feature enablement

## Troubleshooting

### Common Issues

**Access Denied Errors:**
- Check user's branch assignment in Employee table
- Verify branch hierarchy relationships
- Confirm branch type settings

**Performance Issues:**
- Review query execution plans
- Check index utilization
- Monitor audit log size

**Data Inconsistency:**
- Validate parent-child relationships
- Check for orphaned branches
- Verify branch type constraints

### Debugging Tools

**SQL Queries for Investigation:**
```sql
-- Check branch hierarchy
SELECT B1.BranchName AS Child, B2.BranchName AS Parent, B1.BranchType
FROM Branch B1 
LEFT JOIN Branch B2 ON B1.ParentBranchId = B2.BranchId
ORDER BY B2.BranchName, B1.BranchName

-- Find access violations
SELECT UserId, Action, Details, Timestamp
FROM SecurityAuditLog 
WHERE IsSecurityViolation = 1
ORDER BY Timestamp DESC
```

This implementation provides a robust, scalable, and secure branch hierarchy system that maintains data integrity while enabling flexible organizational structures. 