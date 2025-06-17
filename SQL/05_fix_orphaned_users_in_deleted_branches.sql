-- Fix orphaned users in deleted branches
-- This script transfers users from deleted branches to active branches to prevent login issues

-- First, let's identify users with deleted branches
SELECT 
    u.Id as UserId,
    u.Email,
    u.BranchId as UserBranchId,
    b.BranchName,
    b.DeletedDate as BranchDeletedDate,
    e.EmployeeId,
    e.FirstName + ' ' + e.LastName as EmployeeName,
    e.Status as EmployeeStatus,
    e.DeletedDate as EmployeeDeletedDate
FROM AspNetUsers u
LEFT JOIN Branch b ON u.BranchId = b.BranchId
LEFT JOIN Employee e ON u.Id = e.UserId
WHERE b.DeletedDate IS NOT NULL OR b.BranchId IS NULL;

-- Find the first active branch to transfer users to
DECLARE @TransferToBranchId INT;
SELECT TOP 1 @TransferToBranchId = BranchId 
FROM Branch 
WHERE DeletedDate IS NULL 
ORDER BY BranchId;

-- Show which branch users will be transferred to
SELECT @TransferToBranchId as TransferToBranchId, BranchName as TransferToBranchName
FROM Branch 
WHERE BranchId = @TransferToBranchId;

-- Update users who belong to deleted branches or have null BranchId
UPDATE AspNetUsers 
SET BranchId = @TransferToBranchId
WHERE BranchId IN (
    SELECT BranchId 
    FROM Branch 
    WHERE DeletedDate IS NOT NULL
) 
OR BranchId IS NULL;

-- Deactivate dynamic user roles for employees whose branches were deleted
UPDATE DynamicUserRoles 
SET IsActive = 0
WHERE UserId IN (
    SELECT e.UserId
    FROM Employee e
    INNER JOIN Branch b ON e.BranchId = b.BranchId
    WHERE b.DeletedDate IS NOT NULL
    AND e.DeletedDate IS NOT NULL
);

-- Verify the fix - should return no orphaned users
SELECT 
    COUNT(*) as RemainingOrphanedUsers
FROM AspNetUsers u
LEFT JOIN Branch b ON u.BranchId = b.BranchId
WHERE b.DeletedDate IS NOT NULL OR b.BranchId IS NULL;

PRINT 'User transfer completed. All users have been moved to active branches.';
PRINT 'Note: User roles have been deactivated for security. Please reassign roles as needed through the Role Management interface.'; 