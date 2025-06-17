-- =====================================================================================
-- SecurityAuditLogs Table Creation Script
-- Purpose: Create the SecurityAuditLogs table manually without Entity Framework migration
-- Version: 1.0
-- =====================================================================================

-- Use the appropriate database
-- USE [LetsCheckInDb];

-- =====================================================================================
-- CREATE SECURITY AUDIT LOGS TABLE
-- =====================================================================================

-- Check if table already exists and drop if needed (optional - remove if you want to keep existing data)
-- IF OBJECT_ID('SecurityAuditLogs', 'U') IS NOT NULL
--     DROP TABLE SecurityAuditLogs;

-- Create SecurityAuditLogs table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SecurityAuditLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SecurityAuditLogs] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [ResourceType] NVARCHAR(50) NOT NULL,
        [ResourceId] INT NOT NULL,
        [Details] NVARCHAR(1000) NOT NULL DEFAULT '',
        [IPAddress] NVARCHAR(45) NOT NULL DEFAULT '',
        [UserAgent] NVARCHAR(500) NOT NULL DEFAULT '',
        [Timestamp] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
        [Severity] NVARCHAR(20) NOT NULL DEFAULT 'Medium',
        [IsSecurityViolation] BIT NOT NULL DEFAULT 0,
        [SessionId] NVARCHAR(100) NULL,
        [ClientInfo] NVARCHAR(200) NULL,
        [HttpMethod] NVARCHAR(10) NULL,
        [RequestPath] NVARCHAR(500) NULL,
        [Success] BIT NOT NULL DEFAULT 1,
        [ErrorMessage] NVARCHAR(1000) NULL,
        
        -- Primary Key
        CONSTRAINT [PK_SecurityAuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC),
        
        -- Check constraints
        CONSTRAINT [CK_SecurityAuditLog_Severity_Valid] 
            CHECK ([Severity] IN ('Low', 'Medium', 'High', 'Critical')),
        
        -- Foreign Key to AspNetUsers
        CONSTRAINT [FK_SecurityAuditLogs_AspNetUsers_UserId] 
            FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    
    PRINT 'SecurityAuditLogs table created successfully';
END
ELSE
BEGIN
    PRINT 'SecurityAuditLogs table already exists';
END
GO

-- =====================================================================================
-- CREATE INDEXES FOR PERFORMANCE
-- =====================================================================================

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
GO

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
GO

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
GO

-- Timestamp-based queries for recent activity
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAuditLog_Timestamp' AND object_id = OBJECT_ID('SecurityAuditLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SecurityAuditLog_Timestamp] 
    ON [SecurityAuditLogs] ([Timestamp] DESC)
    INCLUDE ([UserId], [Action], [ResourceType], [IsSecurityViolation], [Severity]);
    PRINT 'Created IX_SecurityAuditLog_Timestamp index';
END
ELSE
    PRINT 'IX_SecurityAuditLog_Timestamp index already exists';
GO

-- Resource-specific queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SecurityAuditLog_Resource' AND object_id = OBJECT_ID('SecurityAuditLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SecurityAuditLog_Resource] 
    ON [SecurityAuditLogs] ([ResourceType], [ResourceId], [Timestamp] DESC)
    INCLUDE ([UserId], [Action], [Success], [Details]);
    PRINT 'Created IX_SecurityAuditLog_Resource index';
END
ELSE
    PRINT 'IX_SecurityAuditLog_Resource index already exists';
GO

-- =====================================================================================
-- SAMPLE DATA INSERTION (OPTIONAL - REMOVE IF NOT NEEDED)
-- =====================================================================================

-- Insert a sample audit log entry to verify table is working
-- Note: Replace 'sample-user-id' with an actual user ID from your AspNetUsers table
-- IF NOT EXISTS (SELECT 1 FROM SecurityAuditLogs WHERE Action = 'TABLE_CREATED')
-- BEGIN
--     INSERT INTO SecurityAuditLogs (
--         UserId, Action, ResourceType, ResourceId, Details, 
--         IPAddress, Severity, IsSecurityViolation, Success
--     ) VALUES (
--         'sample-user-id', 'TABLE_CREATED', 'SecurityAuditLogs', 0, 
--         'SecurityAuditLogs table created manually via SQL script',
--         '127.0.0.1', 'Low', 0, 1
--     );
--     PRINT 'Sample audit log entry created';
-- END

-- =====================================================================================
-- COMPLETION MESSAGE
-- =====================================================================================

PRINT '======================================================================================';
PRINT 'SecurityAuditLogs Table Creation Completed Successfully';
PRINT 'The following components have been created:';
PRINT '  - SecurityAuditLogs table with all required columns';
PRINT '  - Primary key and foreign key constraints';
PRINT '  - Check constraints for data validation';
PRINT '  - Performance-optimized indexes';
PRINT '  - Security violation tracking capabilities';
PRINT '======================================================================================';

-- Verify table creation
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'SecurityAuditLogs'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'Table structure verification completed.';
PRINT 'You can now run the main security optimization script (02_update_security_indexes.sql)'; 