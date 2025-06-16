-- Check if migration exists
SELECT * FROM [__EFMigrationsHistory] 
WHERE [MigrationId] = '20250529154033_AddUserRelatedTables';

-- Check if columns exist in Employee table
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Employee'
AND COLUMN_NAME IN ('CreatedDate', 'IsActive');

-- Check if LastLoginDate exists in AspNetUsers table
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AspNetUsers'
AND COLUMN_NAME = 'LastLoginDate'; 