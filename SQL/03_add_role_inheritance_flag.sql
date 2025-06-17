-- Add IsParentOnly flag to Role table for role inheritance functionality
-- This flag marks roles that should only be available to parent accounts (not inherited by children)

-- Add the new column to Role table
ALTER TABLE [dbo].[DynamicRoles] 
ADD [IsParentOnly] BIT NOT NULL DEFAULT 0;
GO

-- Add index for efficient querying of parent-only roles
CREATE NONCLUSTERED INDEX [IX_DynamicRoles_IsParentOnly] 
ON [dbo].[DynamicRoles] ([IsParentOnly]);
GO

-- Add index combining IsParentOnly with IsActive for better performance
CREATE NONCLUSTERED INDEX [IX_DynamicRoles_IsParentOnly_IsActive] 
ON [dbo].[DynamicRoles] ([IsParentOnly], [IsActive]);
GO

-- Update existing system roles to be parent-only by default (SuperAdmin should not be inherited)
UPDATE [dbo].[DynamicRoles] 
SET [IsParentOnly] = 1 
WHERE [RoleName] IN ('SuperAdmin', 'Admin');
GO

-- Verify the changes
SELECT [RoleId], [RoleName], [IsSystemRole], [IsParentOnly], [IsActive] 
FROM [dbo].[DynamicRoles] 
ORDER BY [RoleName];
GO

PRINT 'Role inheritance flag added successfully. Parent-only roles are now marked.'; 