-- Verification: Check Action Tracking Migration Status
-- Purpose: Verify that action tracking fields have been added to LeaveRequest table
-- Date: 2024-12-19

PRINT 'Verifying Action Tracking Migration...';
PRINT '';

-- Check if ActionPerformedBy column exists
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequest') AND name = 'ActionPerformedBy')
BEGIN
    PRINT '✓ ActionPerformedBy column exists';
    
    -- Check column details
    SELECT 
        'ActionPerformedBy Column Details:' as Info,
        c.name AS ColumnName,
        t.name AS DataType,
        c.max_length AS MaxLength,
        c.is_nullable AS IsNullable
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('LeaveRequest') AND c.name = 'ActionPerformedBy';
END
ELSE
BEGIN
    PRINT '✗ ActionPerformedBy column is missing!';
END

PRINT '';

-- Check if ActionPerformedOn column exists
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequest') AND name = 'ActionPerformedOn')
BEGIN
    PRINT '✓ ActionPerformedOn column exists';
    
    -- Check column details
    SELECT 
        'ActionPerformedOn Column Details:' as Info,
        c.name AS ColumnName,
        t.name AS DataType,
        c.precision AS Precision,
        c.scale AS Scale,
        c.is_nullable AS IsNullable
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('LeaveRequest') AND c.name = 'ActionPerformedOn';
END
ELSE
BEGIN
    PRINT '✗ ActionPerformedOn column is missing!';
END

PRINT '';

-- Check if foreign key constraint exists
IF EXISTS (
    SELECT * FROM sys.foreign_keys 
    WHERE name = 'FK_LeaveRequest_ActionPerformedBy_AspNetUsers_Id'
)
BEGIN
    PRINT '✓ Foreign key constraint FK_LeaveRequest_ActionPerformedBy_AspNetUsers_Id exists';
END
ELSE
BEGIN
    PRINT '✗ Foreign key constraint is missing!';
END

-- Check if indexes exist
IF EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('LeaveRequest') 
    AND name = 'IX_LeaveRequest_ActionPerformedBy'
)
BEGIN
    PRINT '✓ Index IX_LeaveRequest_ActionPerformedBy exists';
END
ELSE
BEGIN
    PRINT '✗ Index IX_LeaveRequest_ActionPerformedBy is missing!';
END

IF EXISTS (
    SELECT * FROM sys.indexes 
    WHERE object_id = OBJECT_ID('LeaveRequest') 
    AND name = 'IX_LeaveRequest_ActionPerformedOn'
)
BEGIN
    PRINT '✓ Index IX_LeaveRequest_ActionPerformedOn exists';
END
ELSE
BEGIN
    PRINT '✗ Index IX_LeaveRequest_ActionPerformedOn is missing!';
END

PRINT '';

-- Check if extended properties (comments) exist
IF EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('LeaveRequest') 
    AND minor_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequest') AND name = 'ActionPerformedBy')
)
BEGIN
    PRINT '✓ Extended property for ActionPerformedBy exists';
END
ELSE
BEGIN
    PRINT '✗ Extended property for ActionPerformedBy is missing!';
END

IF EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('LeaveRequest') 
    AND minor_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequest') AND name = 'ActionPerformedOn')
)
BEGIN
    PRINT '✓ Extended property for ActionPerformedOn exists';
END
ELSE
BEGIN
    PRINT '✗ Extended property for ActionPerformedOn is missing!';
END

PRINT '';

-- Show current LeaveRequest table structure
PRINT 'Current LeaveRequest table columns:';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    CASE 
        WHEN c.max_length = -1 THEN 'MAX'
        WHEN t.name IN ('nvarchar', 'nchar') THEN CAST(c.max_length/2 AS VARCHAR(10))
        ELSE CAST(c.max_length AS VARCHAR(10))
    END AS Length,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'Yes' ELSE 'No' END AS IsPrimaryKey
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE pk ON 
    pk.TABLE_NAME = 'LeaveRequest' AND 
    pk.COLUMN_NAME = c.name AND 
    pk.CONSTRAINT_NAME LIKE 'PK_%'
WHERE c.object_id = OBJECT_ID('LeaveRequest')
ORDER BY c.column_id;

PRINT '';
PRINT 'Migration verification completed!'; 