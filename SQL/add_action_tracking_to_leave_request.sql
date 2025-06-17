-- Migration: Add Action Tracking to LeaveRequest Table
-- Purpose: Track who approved/rejected leave requests and when
-- Date: 2024-12-19

-- Add ActionPerformedBy column (UserId of person who approved/rejected)
-- This will be a foreign key to AspNetUsers table
ALTER TABLE LeaveRequests 
ADD ActionPerformedBy NVARCHAR(450) NULL;
GO
-- Add ActionPerformedOn column (DateTime when action was performed)
ALTER TABLE LeaveRequests 
ADD ActionPerformedOn DATETIME2 NULL;
GO
-- Add foreign key constraint for ActionPerformedBy
-- Links to AspNetUsers.Id to track which user performed the action
ALTER TABLE LeaveRequests
ADD CONSTRAINT FK_LeaveRequest_ActionPerformedBy_AspNetUsers_Id
FOREIGN KEY (ActionPerformedBy) REFERENCES AspNetUsers(Id)
ON DELETE SET NULL;  -- If user is deleted, set ActionPerformedBy to NULL

-- Add index on ActionPerformedBy for better query performance
CREATE INDEX IX_LeaveRequest_ActionPerformedBy 
ON LeaveRequests (ActionPerformedBy);

-- Add index on ActionPerformedOn for better query performance when filtering by action date
CREATE INDEX IX_LeaveRequest_ActionPerformedOn 
ON LeaveRequests (ActionPerformedOn);

-- Add comments to document the purpose of these fields
EXEC sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'UserId of the person who approved, rejected, or cancelled this leave request',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'LeaveRequests',
    @level2type = N'COLUMN', @level2name = N'ActionPerformedBy';

EXEC sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Date and time when the approval, rejection, or cancellation action was performed',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'LeaveRequests',
    @level2type = N'COLUMN', @level2name = N'ActionPerformedOn';

-- Print success message
PRINT 'Successfully added action tracking fields to LeaveRequests table';
PRINT 'Added columns:';
PRINT '  - ActionPerformedBy (NVARCHAR(450), nullable, FK to AspNetUsers)';
PRINT '  - ActionPerformedOn (DATETIME2, nullable)';
PRINT 'Added indexes:';
PRINT '  - IX_LeaveRequest_ActionPerformedBy';
PRINT '  - IX_LeaveRequest_ActionPerformedOn';
PRINT 'Migration completed successfully!'; 