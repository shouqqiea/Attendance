using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsCheckIn.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRelatedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First check if the columns exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                              WHERE object_id = OBJECT_ID(N'[dbo].[Employee]')
                              AND name = 'CreatedDate')
                BEGIN
                    ALTER TABLE [dbo].[Employee]
                    ADD [CreatedDate] datetime2(7) NOT NULL DEFAULT GETDATE();

                    -- Update existing records
                    UPDATE [dbo].[Employee]
                    SET [CreatedDate] = GETDATE()
                    WHERE [CreatedDate] IS NULL;
                END;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                              WHERE object_id = OBJECT_ID(N'[dbo].[Employee]')
                              AND name = 'IsActive')
                BEGIN
                    ALTER TABLE [dbo].[Employee]
                    ADD [IsActive] bit NOT NULL DEFAULT 1;

                    -- Update existing records
                    UPDATE [dbo].[Employee]
                    SET [IsActive] = 1
                    WHERE [IsActive] IS NULL;
                END;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                              WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]')
                              AND name = 'LastLoginDate')
                BEGIN
                    ALTER TABLE [dbo].[AspNetUsers]
                    ADD [LastLoginDate] datetime2(7) NULL;
                END;

                -- Ensure the migration is recorded
                IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '20250529154033_AddUserRelatedTables')
                BEGIN
                    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES (N'20250529154033_AddUserRelatedTables', N'8.0.0');
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                BEGIN TRANSACTION;

                -- Remove migration history first
                IF EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '20250529154033_AddUserRelatedTables')
                BEGIN
                    DELETE FROM [dbo].[__EFMigrationsHistory]
                    WHERE [MigrationId] = '20250529154033_AddUserRelatedTables';
                END;

                -- Then remove columns if they exist
                IF EXISTS (SELECT 1 FROM sys.columns 
                          WHERE object_id = OBJECT_ID(N'[dbo].[Employee]')
                          AND name = 'CreatedDate')
                BEGIN
                    ALTER TABLE [dbo].[Employee]
                    DROP COLUMN [CreatedDate];
                END;

                IF EXISTS (SELECT 1 FROM sys.columns 
                          WHERE object_id = OBJECT_ID(N'[dbo].[Employee]')
                          AND name = 'IsActive')
                BEGIN
                    ALTER TABLE [dbo].[Employee]
                    DROP COLUMN [IsActive];
                END;

                IF EXISTS (SELECT 1 FROM sys.columns 
                          WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]')
                          AND name = 'LastLoginDate')
                BEGIN
                    ALTER TABLE [dbo].[AspNetUsers]
                    DROP COLUMN [LastLoginDate];
                END;

                COMMIT;
            ");
        }
    }
}
