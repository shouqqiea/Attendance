-- Quick script to add leave.data permission
-- Run this if you want a simple insert without extensive checking

-- Add the permission
INSERT INTO [Permissions] ([PermissionName], [Description], [Category], [IsActive], [CreatedDate])
VALUES ('leave.data', 'View leave data and reports', 'Leave Management', 1, GETUTCDATE());

-- Get the new permission ID
DECLARE @PermissionId INT = SCOPE_IDENTITY();

-- Assign to SuperAdmin, Admin, and Manager roles
INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [AssignedDate], [AssignedBy], [IsActive])
SELECT 
    r.[RoleId], 
    @PermissionId,
    GETUTCDATE(),
    'System',
    1
FROM [DynamicRoles] r 
WHERE r.[RoleName] IN ('SuperAdmin', 'Admin', 'Manager') 
    AND r.[IsActive] = 1;

-- Verify the assignments
SELECT 
    r.[RoleName],
    p.[PermissionName]
FROM [RolePermissions] rp
INNER JOIN [DynamicRoles] r ON rp.[RoleId] = r.[RoleId]
INNER JOIN [Permissions] p ON rp.[PermissionId] = p.[PermissionId]
WHERE p.[PermissionName] = 'leave.data' 
    AND rp.[IsActive] = 1
ORDER BY r.[RoleName]; 