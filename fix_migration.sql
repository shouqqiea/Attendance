-- Remove the migration history entry if it exists
DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = '20250529154033_AddUserRelatedTables';

-- Add columns if they don't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns 
              WHERE object_id = OBJECT_ID(N'[dbo].[Employee]')
              AND name = 'CreatedDate')
BEGIN
    ALTER TABLE [dbo].[Employee]
    ADD [CreatedDate] datetime2 NOT NULL DEFAULT GETDATE();
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns 
              WHERE object_id = OBJECT_ID(N'[dbo].[Employee]')
              AND name = 'IsActive')
BEGIN
    ALTER TABLE [dbo].[Employee]
    ADD [IsActive] bit NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns 
              WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]')
              AND name = 'LastLoginDate')
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [LastLoginDate] datetime2 NULL;
END;

-- Re-add the migration history entry
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20250529154033_AddUserRelatedTables', '8.0.0'); 