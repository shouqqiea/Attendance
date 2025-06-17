# Business User Guide: Branch Hierarchy System

## Overview

The LetsCheckIn branch hierarchy system allows organizations to structure their branches in a multi-level hierarchy, supporting both Corporate and Single branch types. This guide explains how to use the system effectively for managing your organization's structure.

## Table of Contents

1. [Understanding Branch Types](#understanding-branch-types)
2. [User Roles and Permissions](#user-roles-and-permissions)
3. [Setting Up Your Organization](#setting-up-your-organization)
4. [Managing Branch Hierarchy](#managing-branch-hierarchy)
5. [Common Workflows](#common-workflows)
6. [Reporting and Data Access](#reporting-and-data-access)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)

## Understanding Branch Types

### Branch Type Definitions

#### 🏢 Corporate Branch (Type 1)
- **Purpose**: Manages multiple locations or departments
- **Capabilities**: 
  - Can create and manage child branches
  - Access data from all child branches
  - Assign roles to child branch users
  - Generate reports across the entire hierarchy
- **Best For**: Regional offices, departments, multi-location businesses

#### 🏪 Single Branch (Type 2)
- **Purpose**: Standalone operational unit
- **Capabilities**:
  - Manages only its own employees and data
  - Cannot create child branches
  - Limited to own branch operations
- **Best For**: Individual stores, small offices, independent units

#### 👑 SuperAdmin Branch (Main Branch)
- **Purpose**: Global system administration
- **Capabilities**:
  - Access to all branches regardless of hierarchy
  - Create new root-level branches
  - Manage system-wide settings
  - Override any restrictions
- **Users**: System administrators only

## User Roles and Permissions

### Permission Matrix

| Role | SuperAdmin Branch | Corporate Branch | Single Branch |
|------|-------------------|------------------|---------------|
| **SuperAdmin** | ✅ Global access to all branches | ✅ All operations | ✅ All operations |
| **Admin** | ✅ Manage child branches<br>✅ View child branch data | ✅ Manage child branches<br>✅ View child branch data | ✅ Own branch only<br>❌ Cannot create children |
| **Manager** | ✅ Own branch operations | ✅ Own branch operations | ✅ Own branch operations |
| **Employee** | ✅ Own data only | ✅ Own data only | ✅ Own data only |

### Access Scope Examples

**SuperAdmin on Main Branch:**
- Can see and manage all branches globally
- Create new root-level Corporate or Single branches
- Access all employee data, leave requests, reports

**Admin on Corporate Branch:**
- Manage their own branch and all child branches
- Create new child branches under their branch
- View and approve leave requests from child branches
- Generate reports for entire hierarchy

**Admin on Single Branch:**
- Manage only their own branch
- Cannot create child branches
- Limited to own branch data and operations

## Setting Up Your Organization

### Initial Setup Process

1. **System Installation**: SuperAdmin creates the Main Branch (automatic)
2. **Root Branch Creation**: SuperAdmin creates first-level Corporate branches
3. **Hierarchy Building**: Corporate branch admins create child branches
4. **User Assignment**: Assign employees to appropriate branches with correct roles

### Organization Structure Examples

#### Example 1: Multi-Regional Company
```
Main Branch (SuperAdmin)
├── North Region (Corporate)
│   ├── New York Office (Single)
│   ├── Boston Office (Single)
│   └── Chicago Branch (Corporate)
│       ├── Downtown Location (Single)
│       └── Suburban Location (Single)
├── South Region (Corporate)
│   ├── Miami Office (Single)
│   ├── Atlanta Office (Single)
│   └── Texas Division (Corporate)
│       ├── Dallas Office (Single)
│       └── Houston Office (Single)
```

#### Example 2: Department-Based Organization
```
Main Branch (SuperAdmin)
├── Human Resources (Corporate)
│   ├── Recruitment Team (Single)
│   ├── Training Team (Single)
│   └── Payroll Team (Single)
├── Sales Department (Corporate)
│   ├── Inside Sales (Single)
│   ├── Field Sales (Single)
│   └── Customer Support (Single)
├── IT Department (Corporate)
│   ├── Development Team (Single)
│   ├── Infrastructure Team (Single)
│   └── Security Team (Single)
```

## Managing Branch Hierarchy

### Creating New Branches

#### For SuperAdmin:
1. Navigate to **Branch Management** → **Create Branch**
2. Fill in branch details:
   - **Branch Name**: Descriptive name for the branch
   - **Branch Type**: Choose Corporate or Single
   - **Parent Branch**: Leave empty for root-level branches
   - **Admin Details**: Name and email for branch administrator
3. Configure features (Location, Network, Biometric tracking)
4. Click **Create Branch**

#### For Corporate Branch Admin:
1. Navigate to **Branch Management** → **Create Branch**
2. Fill in branch details:
   - **Branch Name**: Name for the new child branch
   - **Branch Type**: Corporate (can have children) or Single (standalone)
   - **Parent Branch**: Automatically set to your branch
   - **Admin Details**: Administrator information
3. Configure branch features
4. Click **Create Branch**

### Updating Branch Information

1. Go to **Branch Management** → **Branch List**
2. Find your branch in the hierarchy view
3. Click **Edit** button
4. Update allowed fields:
   - Branch name
   - Admin details  
   - Feature settings
   - ⚠️ **Note**: Branch type and parent cannot be changed after creation
5. Save changes

### Branch Hierarchy Navigation

The system displays branches in a tree structure:
- **🏢 Icons**: Indicate Corporate branches
- **🏪 Icons**: Indicate Single branches  
- **Indentation**: Shows hierarchy levels
- **Expand/Collapse**: Click arrows to navigate tree
- **Breadcrumbs**: Show your current location in hierarchy

## Common Workflows

### Workflow 1: Expanding to New Location

**Scenario**: Corporate branch needs to add a new office location

**Steps**:
1. **Corporate Admin** logs into system
2. Navigate to **Create Branch**
3. Select branch type:
   - **Corporate**: If location will have multiple departments/sub-branches
   - **Single**: If location is standalone office
4. Fill in location details and assign local administrator
5. Configure location-specific features
6. **New Branch Admin** receives access and sets up local operations

### Workflow 2: Restructuring Organization

**Scenario**: Need to reorganize branch hierarchy

**Limitation**: ⚠️ Branch parent-child relationships cannot be changed after creation

**Solution**:
1. Plan new structure carefully before implementation
2. Create new branches with correct hierarchy
3. Transfer employees to new branches
4. Deactivate old branches (contact SuperAdmin)
5. Update reporting and access controls

### Workflow 3: Managing Leave Approvals Across Hierarchy

**Scenario**: Corporate branch admin managing leave requests from child branches

**Process**:
1. **Employee** submits leave request in their branch
2. **Local Manager** can approve routine requests
3. **Corporate Admin** sees all child branch requests
4. Approval hierarchy respects branch structure
5. Reports aggregate data across entire corporate hierarchy

### Workflow 4: Creating Departmental Structure

**Scenario**: Large office needs department-based branches

**Steps**:
1. **Corporate Admin** creates department branches:
   - HR Department (Corporate - can have teams)
   - Sales Department (Corporate - can have teams)  
   - IT Department (Single - smaller team)
2. Department heads become **Admins** of their branches
3. Teams/sub-departments created under Corporate departments
4. Employees assigned to appropriate department branches

## Reporting and Data Access

### Data Visibility Rules

#### Corporate Branch Users:
- **Own Branch Data**: Full access to local employees, leave requests, reports
- **Child Branch Data**: Full access to all descendant branches
- **Sibling Branches**: No access (same level branches)
- **Parent Branches**: No access (higher level branches)

#### Single Branch Users:
- **Own Branch Only**: Limited to their assigned branch data
- **No Cross-Branch Access**: Cannot see any other branch data

#### SuperAdmin:
- **Global Access**: Can view and manage all branches and data

### Available Reports

#### Branch Performance Reports:
- Employee attendance across hierarchy
- Leave utilization by branch and department
- Branch comparison analytics
- Hierarchy-based trend analysis

#### Administrative Reports:
- User access patterns
- Branch structure visualization
- Permission assignment audit
- Security access logs

### Data Export Options

**Excel Export**: Complete data with hierarchy information
**CSV Export**: Flat data format for external analysis
**Filtered Views**: Export only accessible branch data

## Best Practices

### Branch Structure Design

#### ✅ Good Practices:
- **Plan Hierarchy Carefully**: Design structure before implementation
- **Use Corporate Types Strategically**: Only when you need child branches
- **Meaningful Names**: Use clear, descriptive branch names
- **Logical Grouping**: Group related functions/locations together

#### ❌ Avoid These Mistakes:
- **Excessive Nesting**: Too many hierarchy levels become hard to manage
- **Wrong Branch Types**: Using Corporate when Single would suffice
- **Poor Naming**: Vague or confusing branch names
- **Frequent Restructuring**: Plan structure to minimize changes

### User Management

#### Role Assignment Guidelines:
- **SuperAdmin**: System administrators only
- **Admin**: Branch managers who need to create/manage child branches
- **Manager**: Departmental supervisors with approval authority
- **Employee**: Regular staff with basic access

#### Security Best Practices:
- **Principle of Least Privilege**: Give users minimum necessary access
- **Regular Access Review**: Audit user permissions quarterly
- **Proper Onboarding**: Assign new users to correct branches and roles
- **Clean Offboarding**: Remove access when employees leave

### Data Management

#### Maintain Data Quality:
- **Consistent Naming**: Use standardized naming conventions
- **Regular Cleanup**: Remove inactive branches and users
- **Backup Strategy**: Ensure data backup includes hierarchy structure
- **Audit Trail**: Monitor access patterns and changes

## Troubleshooting

### Common Issues and Solutions

#### Issue: "Access Denied" When Viewing Data
**Possible Causes**:
- User assigned to wrong branch
- Incorrect role permissions
- Branch hierarchy misconfiguration

**Solutions**:
1. Check user's branch assignment in profile
2. Verify user has appropriate role (Admin, Manager, Employee)
3. Confirm branch hierarchy is correctly structured
4. Contact SuperAdmin if permissions seem incorrect

#### Issue: Cannot Create Child Branch
**Possible Causes**:
- Parent branch is Single type (cannot have children)
- User lacks Admin permissions
- Trying to create root branch without SuperAdmin access

**Solutions**:
1. Verify parent branch is Corporate type
2. Confirm user has Admin role on parent branch
3. For root branches, contact SuperAdmin

#### Issue: Missing Data in Reports
**Possible Causes**:
- User lacks access to specific branches
- Data filtered by hierarchy permissions
- Branch structure blocking access

**Solutions**:
1. Check which branches are accessible to your user
2. Verify you have permissions for target branches
3. Request elevated access if business need exists

#### Issue: Branch Hierarchy Appears Incorrect
**Possible Causes**:
- Browser cache showing old structure
- Database synchronization delay
- Incorrect branch relationships

**Solutions**:
1. Refresh browser and clear cache
2. Wait a few minutes for system synchronization
3. Contact SuperAdmin to verify branch structure

### Getting Help

#### Support Escalation Path:
1. **Self-Service**: Check this guide and system help
2. **Local Admin**: Contact your branch administrator
3. **Corporate Admin**: Escalate to corporate branch admin
4. **SuperAdmin**: Final escalation for system-wide issues

#### Information to Provide When Requesting Help:
- Your username and role
- Branch you're assigned to
- Specific action you're trying to perform
- Error messages received
- Screenshots if helpful

### System Limitations

#### Current Restrictions:
- **Branch Type Changes**: Cannot change after creation
- **Parent Changes**: Cannot move branches to different parents
- **Hierarchy Depth**: Recommended maximum 5 levels deep
- **Bulk Operations**: Limited bulk branch operations

#### Feature Requests:
- Contact SuperAdmin for enhancement requests
- Provide business justification for changes
- Consider workarounds with current functionality

## Conclusion

The branch hierarchy system provides flexible organizational structure management while maintaining security and data integrity. By understanding branch types, user roles, and following best practices, you can effectively manage your organization's structure and access controls.

For additional support or advanced configuration needs, contact your system administrator or refer to the technical documentation.

---

*Last Updated: June 2025*  
*Version: 1.0* 