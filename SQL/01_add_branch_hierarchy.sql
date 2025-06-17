-- =============================================
-- Script: 01_add_branch_hierarchy.sql
-- Description: Add BranchType and ParentBranchId to support branch hierarchy
-- Author: System
-- Date: 2025-06-17
-- Version: 1.0
-- Compatible with: Microsoft SQL Server 2005+
-- =============================================

USE [YourDatabaseName] -- Replace with your actual database name
GO

PRINT 'Starting Branch Hierarchy Implementation...'
GO

-- =============================================
-- Step 1: Add BranchType column
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Branch') AND name = 'BranchType')
BEGIN
    PRINT 'Adding BranchType column...'
    ALTER TABLE [Branch]
    ADD [BranchType] int NOT NULL CONSTRAINT DF_Branch_BranchType DEFAULT 2
    
    PRINT 'BranchType column added successfully.'
END
ELSE
BEGIN
    PRINT 'BranchType column already exists.'
END
GO

-- =============================================
-- Step 2: Add check constraint for BranchType
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Branch_BranchType')
BEGIN
    PRINT 'Adding BranchType check constraint...'
    ALTER TABLE [Branch]
    ADD CONSTRAINT CK_Branch_BranchType CHECK ([BranchType] IN (1, 2))
    
    PRINT 'BranchType check constraint added successfully.'
END
ELSE
BEGIN
    PRINT 'BranchType check constraint already exists.'
END
GO

-- =============================================
-- Step 3: Add ParentBranchId column
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Branch') AND name = 'ParentBranchId')
BEGIN
    PRINT 'Adding ParentBranchId column...'
    ALTER TABLE [Branch]
    ADD [ParentBranchId] int NULL
    
    PRINT 'ParentBranchId column added successfully.'
END
ELSE
BEGIN
    PRINT 'ParentBranchId column already exists.'
END
GO

-- =============================================
-- Step 4: Update BranchName column constraints
-- =============================================
PRINT 'Updating BranchName column constraints...'
ALTER TABLE [Branch]
ALTER COLUMN [BranchName] NVARCHAR(100) NOT NULL
GO

PRINT 'BranchName column constraints updated.'
GO

-- =============================================
-- Step 5: Set default values for existing data
-- =============================================
PRINT 'Setting default values for existing branches...'

-- Set Main Branch (IsSuperAdminBranch = 1) to Corporate type
UPDATE [Branch] 
SET [BranchType] = 1 
WHERE [IsSuperAdminBranch] = 1
GO

PRINT 'Main Branch set to Corporate type.'
PRINT 'All other branches set to Single type (default).'
GO

-- =============================================
-- Step 6: Add foreign key constraint for hierarchy
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Branch_Branch_ParentBranchId')
BEGIN
    PRINT 'Adding foreign key constraint for branch hierarchy...'
    ALTER TABLE [Branch]
    ADD CONSTRAINT FK_Branch_Branch_ParentBranchId 
        FOREIGN KEY ([ParentBranchId]) 
        REFERENCES [Branch]([BranchId])
    
    PRINT 'Foreign key constraint added successfully.'
END
ELSE
BEGIN
    PRINT 'Foreign key constraint already exists.'
END
GO

-- =============================================
-- Step 7: Create performance indexes
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Branch_BranchType')
BEGIN
    PRINT 'Creating index on BranchType...'
    CREATE NONCLUSTERED INDEX [IX_Branch_BranchType] 
    ON [Branch] ([BranchType])
    INCLUDE ([BranchId], [BranchName], [ParentBranchId])
    
    PRINT 'BranchType index created successfully.'
END
ELSE
BEGIN
    PRINT 'BranchType index already exists.'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Branch_ParentBranchId')
BEGIN
    PRINT 'Creating index on ParentBranchId...'
    CREATE NONCLUSTERED INDEX [IX_Branch_ParentBranchId] 
    ON [Branch] ([ParentBranchId])
    INCLUDE ([BranchId], [BranchName], [BranchType])
    
    PRINT 'ParentBranchId index created successfully.'
END
ELSE
BEGIN
    PRINT 'ParentBranchId index already exists.'
END
GO

-- =============================================
-- Step 8: Verify implementation
-- =============================================
PRINT 'Verifying implementation...'
GO

-- Display current branch structure
SELECT 
    BranchId,
    BranchName,
    BranchType,
    CASE 
        WHEN BranchType = 1 THEN 'Corporate Branch'
        WHEN BranchType = 2 THEN 'Single Branch'
        ELSE 'Unknown'
    END AS BranchTypeDescription,
    ParentBranchId,
    IsSuperAdminBranch,
    CreatedDate
FROM [Branch]
ORDER BY BranchId
GO

PRINT 'Current branch structure displayed above.'
PRINT 'Branch Hierarchy Implementation completed successfully!'
GO

-- =============================================
-- Verification queries
-- =============================================

-- Query to check constraint definitions
SELECT 
    cc.name AS ConstraintName,
    cc.definition AS ConstraintDefinition,
    cc.is_disabled
FROM sys.check_constraints cc
INNER JOIN sys.objects o ON cc.parent_object_id = o.object_id
WHERE o.name = 'Branch'
GO

-- Query to check foreign key definitions
SELECT 
    fk.name AS ForeignKeyName,
    tp.name AS ParentTable,
    tr.name AS ReferencedTable,
    fk.is_disabled
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
WHERE tp.name = 'Branch'
GO

-- Query to check index definitions
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    c.name AS ColumnName,
    ic.is_included_column
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name = 'Branch' 
    AND i.name IN ('IX_Branch_BranchType', 'IX_Branch_ParentBranchId')
ORDER BY i.name, ic.key_ordinal
GO

PRINT 'Verification queries completed.'
PRINT 'Branch Hierarchy implementation script finished.'
GO 