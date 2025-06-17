-- Clean up orphaned BranchRole records that point to deleted branches
-- This script deactivates BranchRole records where the referenced branch has been soft-deleted

-- Check for orphaned BranchRole records
SELECT 
    br.BranchRoleId,
    br.BranchId,
    br.RoleId,
    br.IsActive as BranchRoleActive,
    b.BranchName,
    b.DeletedDate as BranchDeletedDate
FROM BranchRoles br
LEFT JOIN Branch b ON br.BranchId = b.BranchId
WHERE br.IsActive = 1 
  AND b.DeletedDate IS NOT NULL;

-- Deactivate orphaned BranchRole records
UPDATE BranchRoles 
SET IsActive = 0
WHERE BranchId IN (
    SELECT BranchId 
    FROM Branch 
    WHERE DeletedDate IS NOT NULL
) 
AND IsActive = 1;

-- Verify the cleanup
SELECT 
    COUNT(*) as RemainingOrphanedRecords
FROM BranchRoles br
LEFT JOIN Branch b ON br.BranchId = b.BranchId
WHERE br.IsActive = 1 
  AND b.DeletedDate IS NOT NULL;

PRINT 'Cleanup completed. All BranchRole records pointing to deleted branches have been deactivated.'; 