-- =====================================================
-- Script: Add leave.data Permission and Role Assignments
-- Description: Adds the new leave.data permission and assigns it to appropriate roles
-- Date: $(date)
-- =====================================================

-- Step 1: Add the new leave.data permission
-- Check if permission already exists to avoid duplicates
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [PermissionName] = 'leave.data')
BEGIN
    INSERT INTO [Permissions] ([PermissionName], [Description], [Category], [IsActive], [CreatedDate])
    VALUES ('leave.data', 'View leave data and reports', 'Leave Management', 1, GETUTCDATE())
    
    PRINT 'Permission leave.data has been added successfully.'
END
ELSE
BEGIN
    PRINT 'Permission leave.data already exists.'
END

-- Step 2: Get the PermissionId for leave.data
DECLARE @LeaveDataPermissionId INT
SELECT @LeaveDataPermissionId = [PermissionId] FROM [Permissions] WHERE [PermissionName] = 'leave.data'

-- Step 3: Assign leave.data permission to SuperAdmin role
DECLARE @SuperAdminRoleId INT
SELECT @SuperAdminRoleId = [RoleId] FROM [DynamicRoles] WHERE [RoleName] = 'SuperAdmin' AND [IsActive] = 1

IF @SuperAdminRoleId IS NOT NULL AND @LeaveDataPermissionId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [RoleId] = @SuperAdminRoleId AND [PermissionId] = @LeaveDataPermissionId)
    BEGIN
        INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [AssignedDate], [AssignedBy], [IsActive])
        VALUES (@SuperAdminRoleId, @LeaveDataPermissionId, GETUTCDATE(), 'System', 1)
        
        PRINT 'Permission leave.data assigned to SuperAdmin role.'
    END
    ELSE
    BEGIN
        PRINT 'Permission leave.data already assigned to SuperAdmin role.'
    END
END

-- Step 4: Assign leave.data permission to Admin role
DECLARE @AdminRoleId INT
SELECT @AdminRoleId = [RoleId] FROM [DynamicRoles] WHERE [RoleName] = 'Admin' AND [IsActive] = 1

IF @AdminRoleId IS NOT NULL AND @LeaveDataPermissionId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [RoleId] = @AdminRoleId AND [PermissionId] = @LeaveDataPermissionId)
    BEGIN
        INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [AssignedDate], [AssignedBy], [IsActive])
        VALUES (@AdminRoleId, @LeaveDataPermissionId, GETUTCDATE(), 'System', 1)
        
        PRINT 'Permission leave.data assigned to Admin role.'
    END
    ELSE
    BEGIN
        PRINT 'Permission leave.data already assigned to Admin role.'
    END
END

-- Step 6: Verification - Show all roles with leave.data permission
PRINT ''
PRINT '=== Verification: Roles with leave.data permission ==='
SELECT 
    r.[RoleName],
    p.[PermissionName],
    rp.[AssignedDate],
    rp.[AssignedBy],
    rp.[IsActive]
FROM [RolePermissions] rp
INNER JOIN [DynamicRoles] r ON rp.[RoleId] = r.[RoleId]
INNER JOIN [Permissions] p ON rp.[PermissionId] = p.[PermissionId]
WHERE p.[PermissionName] = 'leave.data'
    AND rp.[IsActive] = 1
    AND r.[IsActive] = 1
ORDER BY r.[RoleName]

PRINT ''
PRINT 'Script execution completed successfully!'
PRINT 'Note: Employee role does not get leave.data permission by design.'
PRINT 'Employee role only has leave.view and leave.create permissions.' 