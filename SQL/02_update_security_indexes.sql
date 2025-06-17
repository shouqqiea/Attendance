-- =====================================================================================
-- SQL Server Security and Performance Optimization Script
-- Version: 2.1 (Fixed)
-- Purpose: Add comprehensive indexes, constraints, and security optimizations
--          for branch hierarchy and audit logging system
-- =====================================================================================

-- Use the appropriate database
-- USE [LetsCheckInDb];

-- =====================================================================================
-- BRANCH HIERARCHY PERFORMANCE INDEXES
-- =====================================================================================

-- Index for branch hierarchy traversal (if not already exists)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Branch_ParentBranchId_Hierarchy' AND object_id = OBJECT_ID('Branch'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Branch_ParentBranchId_Hierarchy] 
    ON [Branch] ([ParentBranchId], [BranchType], [DeletedDate])
    INCLUDE ([BranchId], [BranchName], [IsSuperAdminBranch])
    WHERE [DeletedDate] IS NULL;
    PRINT 'Created IX_Branch_ParentBranchId_Hierarchy index';
END
ELSE
    PRINT 'IX_Branch_ParentBranchId_Hierarchy index already exists';
GO

-- Composite index for branch filtering queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Branch_Type_Deleted_Composite' AND object_id = OBJECT_ID('Branch'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Branch_Type_Deleted_Composite] 
    ON [Branch] ([BranchType], [DeletedDate])
    INCLUDE ([BranchId], [BranchName], [ParentBranchId], [IsSuperAdminBranch]);
    PRINT 'Created IX_Branch_Type_Deleted_Composite index';
END
ELSE
    PRINT 'IX_Branch_Type_Deleted_Composite index already exists';
GO

-- =====================================================================================
-- EMPLOYEE BRANCH ACCESS INDEXES
-- =====================================================================================

-- Enhanced employee-branch index for hierarchy queries (fixed column name)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Employee_BranchId_Enhanced' AND object_id = OBJECT_ID('Employee'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Employee_BranchId_Enhanced] 
    ON [Employee] ([BranchId], [DeletedDate], [Status])
    INCLUDE ([EmployeeId], [UserId], [FirstName], [LastName], [Email])
    WHERE [DeletedDate] IS NULL;
    PRINT 'Created IX_Employee_BranchId_Enhanced index';
END
ELSE
    PRINT 'IX_Employee_BranchId_Enhanced index already exists';
GO

-- User-Employee lookup optimization (fixed column name)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Employee_UserId_Branch' AND object_id = OBJECT_ID('Employee'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Employee_UserId_Branch] 
    ON [Employee] ([UserId], [DeletedDate])
    INCLUDE ([EmployeeId], [BranchId], [Status])
    WHERE [DeletedDate] IS NULL;
    PRINT 'Created IX_Employee_UserId_Branch index';
END
ELSE
    PRINT 'IX_Employee_UserId_Branch index already exists';
GO

-- =====================================================================================
-- LEAVE REQUEST HIERARCHY INDEXES
-- =====================================================================================

-- Leave requests by employee branch (hierarchy-aware)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LeaveRequests_Employee_Branch' AND object_id = OBJECT_ID('LeaveRequests'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeaveRequests_Employee_Branch] 
    ON [LeaveRequests] ([EmployeeId], [StatusId], [SubmissionDate])
    INCLUDE ([LeaveRequestId], [LeaveTypeId], [StartDate], [EndDate], [LeaveReason]);
    PRINT 'Created IX_LeaveRequests_Employee_Branch index';
END
ELSE
    PRINT 'IX_LeaveRequests_Employee_Branch index already exists';
GO

-- Leave requests status and date filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LeaveRequests_Status_Date' AND object_id = OBJECT_ID('LeaveRequests'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeaveRequests_Status_Date] 
    ON [LeaveRequests] ([StatusId], [StartDate], [EndDate])
    INCLUDE ([EmployeeId], [LeaveTypeId], [SubmissionDate], [LeaveReason]);
    PRINT 'Created IX_LeaveRequests_Status_Date index';
END
ELSE
    PRINT 'IX_LeaveRequests_Status_Date index already exists';
GO

-- =====================================================================================
-- SECURITY AUDIT LOG INDEXES (Only if table exists)
-- =====================================================================================

-- Check if SecurityAuditLogs table exists before creating indexes
IF OBJECT_ID('SecurityAuditLogs', 'U') IS NOT NULL
BEGIN
    -- Primary security audit index for user activity tracking
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAuditLog_User_Time' AND object_id = OBJECT_ID('SecurityAuditLogs'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_SecurityAuditLog_User_Time] 
        ON [SecurityAuditLogs] ([UserId], [Timestamp] DESC)
        INCLUDE ([Action], [ResourceType], [ResourceId], [IsSecurityViolation], [Success]);
        PRINT 'Created IX_SecurityAuditLog_User_Time index';
    END
    ELSE
        PRINT 'IX_SecurityAuditLog_User_Time index already exists';

    -- Security violation detection index
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAuditLog_Violations' AND object_id = OBJECT_ID('SecurityAuditLogs'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_SecurityAuditLog_Violations] 
        ON [SecurityAuditLogs] ([IsSecurityViolation], [Timestamp] DESC, [Severity])
        INCLUDE ([UserId], [Action], [ResourceType], [ResourceId], [Details])
        WHERE [IsSecurityViolation] = 1;
        PRINT 'Created IX_SecurityAuditLog_Violations index';
    END
    ELSE
        PRINT 'IX_SecurityAuditLog_Violations index already exists';

    -- Action-based audit queries
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAuditLog_Action_Resource' AND object_id = OBJECT_ID('SecurityAuditLogs'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_SecurityAuditLog_Action_Resource] 
        ON [SecurityAuditLogs] ([Action], [ResourceType], [Timestamp] DESC)
        INCLUDE ([UserId], [ResourceId], [Success], [Details]);
        PRINT 'Created IX_SecurityAuditLog_Action_Resource index';
    END
    ELSE
        PRINT 'IX_SecurityAuditLog_Action_Resource index already exists';
END
ELSE
BEGIN
    PRINT 'SecurityAuditLogs table does not exist. Run Entity Framework migrations first.';
    PRINT 'Execute: dotnet ef database update';
END
GO

-- =====================================================================================
-- DYNAMIC ROLES AND PERMISSIONS INDEXES
-- =====================================================================================

-- User role lookup optimization
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DynamicUserRoles_User_Active' AND object_id = OBJECT_ID('DynamicUserRoles'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DynamicUserRoles_User_Active] 
    ON [DynamicUserRoles] ([UserId], [IsActive])
    INCLUDE ([RoleId], [AssignedDate], [AssignedBy]);
    PRINT 'Created IX_DynamicUserRoles_User_Active index';
END
ELSE
    PRINT 'IX_DynamicUserRoles_User_Active index already exists';
GO

-- Branch role assignment queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BranchRoles_Branch_Active' AND object_id = OBJECT_ID('BranchRoles'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BranchRoles_Branch_Active] 
    ON [BranchRoles] ([BranchId], [IsActive])
    INCLUDE ([RoleId], [AssignedDate], [AssignedBy]);
    PRINT 'Created IX_BranchRoles_Branch_Active index';
END
ELSE
    PRINT 'IX_BranchRoles_Branch_Active index already exists';
GO

-- Role permission lookup
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RolePermissions_Role_Permission' AND object_id = OBJECT_ID('RolePermissions'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RolePermissions_Role_Permission] 
    ON [RolePermissions] ([RoleId], [PermissionId])
    INCLUDE ([AssignedDate], [AssignedBy]);
    PRINT 'Created IX_RolePermissions_Role_Permission index';
END
ELSE
    PRINT 'IX_RolePermissions_Role_Permission index already exists';
GO

-- =====================================================================================
-- LEAVE TYPES BRANCH FILTERING
-- =====================================================================================

-- Leave types by branch for dropdown population
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LeaveTypes_Branch_Active' AND object_id = OBJECT_ID('LeaveTypes'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeaveTypes_Branch_Active] 
    ON [LeaveTypes] ([BranchId], [IsActive], [DeletedDate])
    INCLUDE ([LeaveTypeId], [Name], [DefaultDays], [Description])
    WHERE [DeletedDate] IS NULL AND [IsActive] = 1;
    PRINT 'Created IX_LeaveTypes_Branch_Active index';
END
ELSE
    PRINT 'IX_LeaveTypes_Branch_Active index already exists';
GO

-- =====================================================================================
-- PERFORMANCE CONSTRAINTS AND OPTIMIZATIONS
-- =====================================================================================

-- Add check constraint for branch hierarchy consistency
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Branch_Hierarchy_SelfReference')
BEGIN
    ALTER TABLE [Branch] 
    ADD CONSTRAINT [CK_Branch_Hierarchy_SelfReference] 
    CHECK ([BranchId] != [ParentBranchId]);
    PRINT 'Added CK_Branch_Hierarchy_SelfReference constraint';
END
ELSE
    PRINT 'CK_Branch_Hierarchy_SelfReference constraint already exists';
GO

-- Add check constraint for branch type validation
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Branch_Type_Valid')
BEGIN
    ALTER TABLE [Branch] 
    ADD CONSTRAINT [CK_Branch_Type_Valid] 
    CHECK ([BranchType] IN (1, 2)); -- 1 = Corporate, 2 = Single
    PRINT 'Added CK_Branch_Type_Valid constraint';
END
ELSE
    PRINT 'CK_Branch_Type_Valid constraint already exists';
GO

-- Add check constraint for security audit log severity (only if table exists)
IF OBJECT_ID('SecurityAuditLogs', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_SecurityAuditLog_Severity_Valid')
    BEGIN
        ALTER TABLE [SecurityAuditLogs] 
        ADD CONSTRAINT [CK_SecurityAuditLog_Severity_Valid] 
        CHECK ([Severity] IN ('Low', 'Medium', 'High', 'Critical'));
        PRINT 'Added CK_SecurityAuditLog_Severity_Valid constraint';
    END
    ELSE
        PRINT 'CK_SecurityAuditLog_Severity_Valid constraint already exists';
END
ELSE
    PRINT 'SecurityAuditLogs table does not exist - skipping severity constraint';
GO

-- =====================================================================================
-- STORED PROCEDURES FOR HIERARCHY QUERIES
-- =====================================================================================

-- Stored procedure to get all child branches recursively
IF OBJECT_ID('sp_GetChildBranches', 'P') IS NOT NULL
    DROP PROCEDURE sp_GetChildBranches;
GO

CREATE PROCEDURE sp_GetChildBranches
    @ParentBranchId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Fixed CTE syntax with proper semicolon
    WITH BranchHierarchy AS (
        -- Anchor: Get the parent branch
        SELECT 
            BranchId,
            BranchName,
            BranchType,
            ParentBranchId,
            0 as Level,
            CAST(BranchName AS NVARCHAR(MAX)) as Path
        FROM Branch 
        WHERE BranchId = @ParentBranchId 
        AND DeletedDate IS NULL
        
        UNION ALL
        
        -- Recursive: Get child branches
        SELECT 
            b.BranchId,
            b.BranchName,
            b.BranchType,
            b.ParentBranchId,
            bh.Level + 1,
            bh.Path + ' > ' + b.BranchName
        FROM Branch b
        INNER JOIN BranchHierarchy bh ON b.ParentBranchId = bh.BranchId
        WHERE b.DeletedDate IS NULL
    )
    SELECT * FROM BranchHierarchy
    ORDER BY Level, BranchName;
END;
GO

PRINT 'Created sp_GetChildBranches stored procedure';

-- Stored procedure to get accessible branches for a user
IF OBJECT_ID('sp_GetUserAccessibleBranches', 'P') IS NOT NULL
    DROP PROCEDURE sp_GetUserAccessibleBranches;
GO

CREATE PROCEDURE sp_GetUserAccessibleBranches
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Get user's branch
    DECLARE @UserBranchId INT;
    DECLARE @IsSuperAdmin BIT = 0;
    
    SELECT 
        @UserBranchId = e.BranchId,
        @IsSuperAdmin = CASE WHEN b.IsSuperAdminBranch = 1 AND EXISTS (
            SELECT 1 FROM DynamicUserRoles dur 
            INNER JOIN DynamicRoles dr ON dur.RoleId = dr.RoleId 
            WHERE dur.UserId = @UserId 
            AND dr.RoleName = 'SuperAdmin' 
            AND dur.IsActive = 1 
            AND dr.IsActive = 1
        ) THEN 1 ELSE 0 END
    FROM Employee e
    INNER JOIN Branch b ON e.BranchId = b.BranchId
    WHERE e.UserId = @UserId AND e.DeletedDate IS NULL;
    
    -- If SuperAdmin on superadmin branch, return all branches
    IF @IsSuperAdmin = 1
    BEGIN
        SELECT BranchId, BranchName, BranchType, ParentBranchId
        FROM Branch 
        WHERE DeletedDate IS NULL
        ORDER BY BranchName;
        RETURN;
    END;
    
    -- Get user's branch and all descendant branches if Corporate
    -- Fixed CTE syntax with proper semicolon before WITH
    WITH AccessibleBranches AS (
        -- User's own branch
        SELECT BranchId, BranchName, BranchType, ParentBranchId
        FROM Branch 
        WHERE BranchId = @UserBranchId AND DeletedDate IS NULL
        
        UNION ALL
        
        -- Child branches (only if user's branch is Corporate)
        SELECT b.BranchId, b.BranchName, b.BranchType, b.ParentBranchId
        FROM Branch b
        INNER JOIN AccessibleBranches ab ON b.ParentBranchId = ab.BranchId
        WHERE b.DeletedDate IS NULL 
        AND EXISTS (SELECT 1 FROM Branch WHERE BranchId = @UserBranchId AND BranchType = 1) -- Corporate
    )
    SELECT * FROM AccessibleBranches
    ORDER BY BranchName;
END;
GO

PRINT 'Created sp_GetUserAccessibleBranches stored procedure';

-- =====================================================================================
-- STATISTICS AND MAINTENANCE
-- =====================================================================================

-- Update statistics on key tables
UPDATE STATISTICS [Branch];
UPDATE STATISTICS [Employee]; 
UPDATE STATISTICS [LeaveRequests];
UPDATE STATISTICS [DynamicUserRoles];
UPDATE STATISTICS [BranchRoles];

-- Update SecurityAuditLogs statistics only if table exists
IF OBJECT_ID('SecurityAuditLogs', 'U') IS NOT NULL
BEGIN
    UPDATE STATISTICS [SecurityAuditLogs];
    PRINT 'Updated SecurityAuditLogs statistics';
END
ELSE
    PRINT 'SecurityAuditLogs table does not exist - skipping statistics update';

PRINT 'Updated statistics on key tables';

-- =====================================================================================
-- SECURITY OPTIMIZATION VIEWS
-- =====================================================================================

-- Create view for efficient user branch access checking
IF OBJECT_ID('vw_UserBranchAccess', 'V') IS NOT NULL
    DROP VIEW vw_UserBranchAccess;
GO

CREATE VIEW vw_UserBranchAccess AS
SELECT 
    e.UserId,
    e.BranchId as UserBranchId,
    b.BranchName as UserBranchName,
    b.BranchType as UserBranchType,
    b.IsSuperAdminBranch,
    CASE 
        WHEN b.IsSuperAdminBranch = 1 AND EXISTS (
            SELECT 1 FROM DynamicUserRoles dur 
            INNER JOIN DynamicRoles dr ON dur.RoleId = dr.RoleId 
            WHERE dur.UserId = e.UserId 
            AND dr.RoleName = 'SuperAdmin' 
            AND dur.IsActive = 1 
            AND dr.IsActive = 1
        ) THEN 1 
        ELSE 0 
    END as IsSuperAdmin,
    b.BranchType as CanAccessChildBranches -- 1 = Corporate (can access children), 2 = Single (cannot)
FROM Employee e
INNER JOIN Branch b ON e.BranchId = b.BranchId
WHERE e.DeletedDate IS NULL AND b.DeletedDate IS NULL;
GO

PRINT 'Created vw_UserBranchAccess view';

-- =====================================================================================
-- COMPLETION MESSAGE
-- =====================================================================================

PRINT '======================================================================================';
PRINT 'Security and Performance Optimization Script Completed Successfully';
PRINT 'The following optimizations have been applied:';
PRINT '  - Enhanced branch hierarchy indexes for fast traversal';
PRINT '  - Employee-branch relationship optimization';
PRINT '  - Leave request filtering performance indexes';
PRINT '  - Comprehensive security audit log indexes (if table exists)';
PRINT '  - Role and permission lookup optimizations';
PRINT '  - Data integrity constraints';
PRINT '  - Efficient stored procedures for hierarchy queries';
PRINT '  - Optimized views for security checks';
PRINT '======================================================================================';

-- Generate performance report (only for existing tables)
SELECT 
    'Branch' as TableName,
    COUNT(*) as RecordCount,
    COUNT(CASE WHEN DeletedDate IS NULL THEN 1 END) as ActiveRecords
FROM Branch
UNION ALL
SELECT 
    'Employee' as TableName,
    COUNT(*) as RecordCount,
    COUNT(CASE WHEN DeletedDate IS NULL THEN 1 END) as ActiveRecords
FROM Employee
UNION ALL
SELECT 
    'LeaveRequests' as TableName,
    COUNT(*) as RecordCount,
    COUNT(*) as ActiveRecords -- Leave requests don't have DeletedDate
FROM LeaveRequests
UNION ALL
SELECT 
    'SecurityAuditLogs' as TableName,
    CASE WHEN OBJECT_ID('SecurityAuditLogs', 'U') IS NOT NULL THEN COUNT(*) ELSE 0 END as RecordCount,
    CASE WHEN OBJECT_ID('SecurityAuditLogs', 'U') IS NOT NULL THEN 
        COUNT(CASE WHEN Timestamp > DATEADD(day, -30, GETDATE()) THEN 1 END) 
    ELSE 0 END as RecentRecords
FROM (SELECT 1 as dummy) d
LEFT JOIN SecurityAuditLogs sal ON OBJECT_ID('SecurityAuditLogs', 'U') IS NOT NULL;

PRINT 'Performance optimization and security indexes installation completed.';
PRINT '';
PRINT 'IMPORTANT: If SecurityAuditLogs table does not exist, run the following:';
PRINT '  1. dotnet ef database update';
PRINT '  2. Re-run this script to create the security audit indexes'; 