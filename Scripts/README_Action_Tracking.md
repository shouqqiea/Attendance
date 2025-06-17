# Action Tracking Migration for Leave Requests

This directory contains SQL scripts to add action tracking functionality to the LeaveRequest table, allowing the system to track who approved, rejected, or cancelled leave requests and when these actions were performed.

## Files Included

1. **add_action_tracking_to_leave_request.sql** - Main migration script
2. **rollback_action_tracking_from_leave_request.sql** - Rollback script
3. **verify_action_tracking_migration.sql** - Verification script

## What This Migration Does

The migration adds the following fields to the `LeaveRequest` table:

- **ActionPerformedBy** (`NVARCHAR(450)`, nullable) - Foreign key to `AspNetUsers.Id`
- **ActionPerformedOn** (`DATETIME2`, nullable) - Timestamp when the action was performed

### Additional Components Created:

- **Foreign Key Constraint**: `FK_LeaveRequest_ActionPerformedBy_AspNetUsers_Id`
- **Indexes**: 
  - `IX_LeaveRequest_ActionPerformedBy` - For performance when querying by action performer
  - `IX_LeaveRequest_ActionPerformedOn` - For performance when filtering by action date
- **Extended Properties**: Documentation comments for the new columns

## How to Apply the Migration

### Option 1: Using Entity Framework (Recommended)
```bash
# If the EF migration was created successfully
dotnet ef database update
```

### Option 2: Manual SQL Execution
```sql
-- Execute the migration script in SQL Server Management Studio or similar tool
-- File: add_action_tracking_to_leave_request.sql
```

## How to Verify the Migration

Run the verification script to ensure all components were created successfully:

```sql
-- File: verify_action_tracking_migration.sql
```

The verification script will check for:
- ✓ Column existence and correct data types
- ✓ Foreign key constraint
- ✓ Indexes for performance
- ✓ Extended properties (documentation)
- Show complete table structure

## How to Rollback (If Needed)

⚠️ **WARNING**: Rolling back will permanently delete all action tracking data!

```sql
-- File: rollback_action_tracking_from_leave_request.sql
```

## Expected Application Behavior

After applying this migration:

1. **Existing Data**: All existing leave requests will have `NULL` values for the new fields
2. **New Approvals/Rejections**: Will automatically populate action tracking information
3. **Views Updated**: All leave management views will display action performer information
4. **Exports Enhanced**: Excel and CSV exports will include action tracking columns

## Database Schema Changes

```sql
-- New columns added to LeaveRequest table
ActionPerformedBy NVARCHAR(450) NULL  -- FK to AspNetUsers.Id
ActionPerformedOn DATETIME2 NULL      -- When action was performed

-- New foreign key constraint
CONSTRAINT FK_LeaveRequest_ActionPerformedBy_AspNetUsers_Id
FOREIGN KEY (ActionPerformedBy) REFERENCES AspNetUsers(Id)
ON DELETE SET NULL;

-- New indexes for performance
IX_LeaveRequest_ActionPerformedBy
IX_LeaveRequest_ActionPerformedOn
```

## Application Code Changes

The following components have been updated to support action tracking:

### Models
- `LeaveRequest.cs` - Added ActionPerformedBy and ActionPerformedOn properties
- `LeaveRequestViewModel.cs` - Added action tracking fields
- `LeaveDataViewModel.cs` - Added action tracking fields

### Controllers
- `LeaveManagementController.cs` - Updated all queries and action methods
  - Approve/Reject methods now save action tracking info
  - All data queries include ActionPerformer navigation
  - Export methods include action tracking in output

### Views
- `LeaveApproval.cshtml` - Shows action tracking in history section
- `LeaveRequest.cshtml` - Shows action tracking in leave history
- `LeaveData.cshtml` - Added "Action By" column and mobile card info

## Performance Considerations

- Indexes have been added on both new columns for optimal query performance
- The foreign key relationship uses `ON DELETE SET NULL` to maintain data integrity
- Queries are optimized to efficiently load action performer information

## Testing the Implementation

1. **Apply Migration**: Use verification script to confirm successful application
2. **Test Approval Flow**: Approve a leave request and verify action tracking is saved
3. **Test Rejection Flow**: Reject a leave request and verify action tracking is saved
4. **Test Views**: Check that all three main views display action tracking information
5. **Test Exports**: Verify Excel and CSV exports include action tracking columns

## Troubleshooting

### Common Issues:
1. **Foreign Key Constraint Errors**: Ensure all referenced users exist in AspNetUsers table
2. **Index Creation Errors**: Check for sufficient database permissions
3. **Extended Property Errors**: These are non-critical and can be ignored if they fail

### Recovery Steps:
1. Run verification script to identify missing components
2. Re-run specific parts of the migration script as needed
3. If needed, use rollback script and start over

## Support

For questions or issues with this migration, please refer to the application documentation or contact the development team. 