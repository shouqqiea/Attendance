-- Rollback: Remove Action Tracking from LeaveRequest Table
-- Purpose: Rollback the action tracking migration if needed
-- Date: 2024-12-19
-- Warning: This will remove all action tracking data permanently!

-- Print warning message
PRINT 'WARNING: This will permanently remove all action tracking data!';
PRINT 'Proceeding with rollback...';

-- Remove indexes first
DROP INDEX IF EXISTS IX_LeaveRequest_ActionPerformedBy ON LeaveRequest;
DROP INDEX IF EXISTS IX_LeaveRequest_ActionPerformedOn ON LeaveRequest;

-- Remove foreign key constraint
ALTER TABLE LeaveRequest
DROP CONSTRAINT IF EXISTS FK_LeaveRequest_ActionPerformedBy_AspNetUsers_Id;

-- Remove extended properties (comments)
IF EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('LeaveRequest') 
    AND minor_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequest') AND name = 'ActionPerformedBy')
)
BEGIN
    EXEC sp_dropextendedproperty 
        @name = N'MS_Description',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'LeaveRequest',
        @level2type = N'COLUMN', @level2name = N'ActionPerformedBy';
END

IF EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('LeaveRequest') 
    AND minor_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID('LeaveRequest') AND name = 'ActionPerformedOn')
)
BEGIN
    EXEC sp_dropextendedproperty 
        @name = N'MS_Description',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'LeaveRequest',
        @level2type = N'COLUMN', @level2name = N'ActionPerformedOn';
END

-- Remove columns
ALTER TABLE LeaveRequest 
DROP COLUMN IF EXISTS ActionPerformedBy;

ALTER TABLE LeaveRequest 
DROP COLUMN IF EXISTS ActionPerformedOn;

-- Print success message
PRINT 'Successfully removed action tracking fields from LeaveRequest table';
PRINT 'Removed columns:';
PRINT '  - ActionPerformedBy';
PRINT '  - ActionPerformedOn';
PRINT 'Removed indexes:';
PRINT '  - IX_LeaveRequest_ActionPerformedBy';
PRINT '  - IX_LeaveRequest_ActionPerformedOn';
PRINT 'Rollback completed successfully!'; 