using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LetsCheckIn.Models;
using LetsCheckIn.Models.db;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Logging;
using LetsCheckIn.Helpers;
using ClosedXML.Excel;
using System.Text;

namespace LetsCheckIn.Controllers
{
    // ✅ Use permission-based authorization instead of hardcoded role names
    // This allows any user with leave management permissions to access these actions
    [DynamicPermissionAuthorize("leave.view")]
    public class LeaveManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<LeaveManagementController> _logger;
        private readonly IBranchAccessService _branchAccessService;

        public LeaveManagementController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment webHostEnvironment, ILogger<LeaveManagementController> logger, IBranchAccessService branchAccessService)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _branchAccessService = branchAccessService;
        }

        private async Task<StatusType> GetStatusTypeByNameAsync(string statusName)
        {
            return await _context.StatusTypes.FirstOrDefaultAsync(s => s.StatusName == statusName)
                ?? throw new InvalidOperationException($"Status type '{statusName}' not found");
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var employee = await _context.Employee
                .FirstOrDefaultAsync(e => e.UserId == user.Id);
            if (employee == null) return NotFound("Employee record not found");

            // Get leave balances
            var approvedStatusId = (await GetStatusTypeByNameAsync("approved")).StatusId;
            var leaveBalances = await _context.LeaveTypes
                .Where(lt => lt.IsActive && lt.BranchId == employee.BranchId)
                .Select(lt => new LeaveBalanceViewModel(lt.Name)
                {
                    Total = lt.DefaultDays,
                    Used = _context.LeaveRequests.Count(lr => 
                        lr.EmployeeId == employee.EmployeeId && 
                        lr.LeaveTypeId == lt.LeaveTypeId && 
                        lr.StatusId == approvedStatusId &&
                        lr.StartDate.Year == DateTime.Now.Year),
                    Remaining = lt.DefaultDays - _context.LeaveRequests.Count(lr => 
                        lr.EmployeeId == employee.EmployeeId && 
                        lr.LeaveTypeId == lt.LeaveTypeId && 
                        lr.StatusId == approvedStatusId &&
                        lr.StartDate.Year == DateTime.Now.Year)
                })
                .ToListAsync();

            // Get recent leave requests
            var leaveRequests = await _context.LeaveRequests
                .Include(lr => lr.LeaveType)
                .Include(lr => lr.Status)
                .Where(lr => lr.EmployeeId == employee.EmployeeId)
                .OrderByDescending(lr => lr.SubmissionDate)
                .Take(5)
                .Select(lr => new LeaveRequestViewModel(lr.LeaveType.Name, lr.LeaveReason ?? "No reason provided")
                {
                    Id = lr.LeaveRequestId,
                    StartDate = lr.StartDate,
                    EndDate = lr.EndDate,
                    Status = lr.Status.StatusName,
                    SubmittedOn = lr.SubmissionDate
                })
                .ToListAsync();

            ViewBag.LeaveBalances = leaveBalances;
            ViewBag.LeaveRequests = leaveRequests;
            return View();
        }

        public async Task<IActionResult> LeaveRequest()
        {
            try 
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

                var employee = await _context.Employee
                    .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.UserId == user.Id);
            if (employee == null) return NotFound("Employee record not found");

            // Get leave types for dropdown
                var leaveTypes = await _context.LeaveTypes
                    .Where(lt => lt.BranchId == employee.BranchId && lt.IsActive && lt.DeletedDate == null)
                    .OrderBy(lt => lt.Name)
                    .Select(lt => new LeaveTypeViewModel
                    {
                        LeaveTypeId = lt.LeaveTypeId,
                        Name = lt.Name,
                        DefaultDays = lt.DefaultDays,
                        Description = lt.Description,
                        IsActive = lt.IsActive,
                        BranchId = lt.BranchId
                    })
                .ToListAsync();

                                // Get user's leave requests with leave type details
                var leaveRequests = await _context.LeaveRequests
                .Include(lr => lr.LeaveType)
                    .Include(lr => lr.Status)
                    .Include(lr => lr.ActionPerformer)
                    .ThenInclude(ap => ap.Employee)
                .Where(lr => lr.EmployeeId == employee.EmployeeId)
                .OrderByDescending(lr => lr.SubmissionDate)
                    .Select(lr => new LeaveRequestViewModel(lr.LeaveType.Name, lr.LeaveReason ?? "No reason provided")
                {
                    Id = lr.LeaveRequestId,
                    StartDate = lr.StartDate,
                    EndDate = lr.EndDate,
                        Status = lr.Status.StatusName,
                        SubmittedOn = lr.SubmissionDate,
                        AttachmentFileName = lr.AttachmentPath,
                        RejectionReason = lr.RejectedReason,
                        ActionPerformedBy = lr.ActionPerformer != null && lr.ActionPerformer.Employee != null 
                            ? $"{lr.ActionPerformer.Employee.FirstName} {lr.ActionPerformer.Employee.LastName}"
                            : null,
                        ActionPerformedOn = lr.ActionPerformedOn
                    })
                    .ToListAsync();

                // Get leave balances for each leave type
                var approvedStatusId = (await GetStatusTypeByNameAsync("approved")).StatusId;
                var leaveBalances = await _context.LeaveTypes
                    .Where(lt => lt.BranchId == employee.BranchId && lt.IsActive && lt.DeletedDate == null)
                    .Select(lt => new LeaveBalanceViewModel(lt.Name)
                    {
                        Total = lt.DefaultDays,
                        Used = _context.LeaveRequests.Count(lr => 
                            lr.EmployeeId == employee.EmployeeId && 
                            lr.LeaveTypeId == lt.LeaveTypeId && 
                            lr.StatusId == approvedStatusId &&
                            lr.StartDate.Year == DateTime.Now.Year),
                        Remaining = lt.DefaultDays - _context.LeaveRequests.Count(lr => 
                            lr.EmployeeId == employee.EmployeeId && 
                            lr.LeaveTypeId == lt.LeaveTypeId && 
                            lr.StatusId == approvedStatusId &&
                            lr.StartDate.Year == DateTime.Now.Year)
                })
                .ToListAsync();

                ViewBag.LeaveTypes = leaveTypes;
            ViewBag.LeaveRequests = leaveRequests;
                ViewBag.LeaveBalances = leaveBalances;

            return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in LeaveRequest action: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveRequest([FromForm] LeaveRequestViewModel model, IFormFile? attachment)
        {
            try
            {
                _logger.LogInformation($"Received leave request: Type={model.LeaveType}, StartDate={model.StartDate}, EndDate={model.EndDate}, Emergency={model.Emergency}, Reason={model.Reason}");

            var user = await _userManager.GetUserAsync(User);
                if (user == null) 
                {
                    _logger.LogWarning("User not found");
                    return Json(new { success = false, message = "User not found" });
                }

                var employee = await _context.Employee
                .FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null)
                {
                    _logger.LogWarning($"Employee not found for user {user.Id}");
                    return Json(new { success = false, message = "Employee record not found" });
                }

                // Populate the employee information
                model.EmployeeName = $"{employee.FirstName} {employee.LastName}";
                model.EmployeeEmail = employee.Email;

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    
                    // Log each validation error separately
                    foreach (var error in errors)
                    {
                        _logger.LogWarning($"Validation error: {error}");
                    }

                    // Log the model state for debugging
                    foreach (var entry in ModelState)
                    {
                        if (entry.Value.Errors.Any())
                        {
                            _logger.LogWarning($"Field: {entry.Key}, Errors: {string.Join(", ", entry.Value.Errors.Select(e => e.ErrorMessage))}");
                        }
                    }

                    return Json(new { 
                        success = false, 
                        message = "Validation failed", 
                        errors = errors,
                        modelState = ModelState.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToList()
                        )
                    });
                }

                if (!model.StartDate.HasValue || !model.EndDate.HasValue)
                {
                    return Json(new { success = false, message = "Start and end dates are required." });
                }

                if (!int.TryParse(model.LeaveType, out int leaveTypeId))
                {
                    return Json(new { success = false, message = "Invalid leave type selected." });
                }

                var leaveType = await _context.LeaveTypes
                    .FirstOrDefaultAsync(lt => lt.LeaveTypeId == leaveTypeId && lt.IsActive && lt.DeletedDate == null);
                
                if (leaveType == null)
                {
                    _logger.LogWarning($"Leave type {leaveTypeId} not found or not active");
                    return Json(new { success = false, message = "Selected leave type is not valid or no longer active." });
                }

                var approvedStatusId = (await GetStatusTypeByNameAsync("approved")).StatusId;
                var usedLeaves = await _context.LeaveRequests
                    .CountAsync(lr => 
                        lr.EmployeeId == employee.EmployeeId && 
                        lr.LeaveTypeId == leaveType.LeaveTypeId && 
                        lr.StatusId == approvedStatusId &&
                        lr.StartDate.Year == DateTime.Now.Year);

                var remainingLeaves = leaveType.DefaultDays - usedLeaves;
                var requestedDays = (model.EndDate.Value - model.StartDate.Value).Days + 1;

                if (requestedDays > remainingLeaves && model.Emergency != "yes")
                {
                    return Json(new { success = false, message = $"You only have {remainingLeaves} days remaining for {leaveType.Name}." });
                }

                var statusType = await GetStatusTypeByNameAsync(model.Emergency == "yes" ? "emergency" : "pending");
            var leaveRequest = new LeaveRequest
            {
                EmployeeId = employee.EmployeeId,
                    LeaveTypeId = leaveTypeId,
                StartDate = model.StartDate.Value,
                EndDate = model.EndDate.Value,
                    StatusId = statusType.StatusId,
                    LeaveReason = model.Reason,
                SubmissionDate = DateTime.Now
            };

            _context.LeaveRequests.Add(leaveRequest);
            await _context.SaveChangesAsync();

            if (attachment != null && attachment.Length > 0)
            {
                    try
                    {
                        var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "leave-attachments");
                        Directory.CreateDirectory(uploadsDir);

                        var uniqueFileName = $"{leaveRequest.LeaveRequestId}_{DateTime.Now.Ticks}_{Path.GetFileName(attachment.FileName)}";
                        var filePath = Path.Combine(uploadsDir, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await attachment.CopyToAsync(stream);
                        }

                        leaveRequest.AttachmentPath = uniqueFileName;
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error saving attachment: {ex.Message}");
                    }
                }

                return Json(new { success = true, message = "Leave request submitted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in LeaveRequest: {ex.Message}\nStack trace: {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while submitting the request.", error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelLeaveRequest([FromBody] int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var employee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null) return NotFound("Employee record not found");

                var leaveRequest = await _context.LeaveRequests
                    .Include(lr => lr.Status)
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == id && lr.EmployeeId == employee.EmployeeId);

                if (leaveRequest == null)
                {
                    return Json(new { success = false, message = "Leave request not found." });
                }

                if (leaveRequest.Status.StatusName != "pending" && leaveRequest.Status.StatusName != "emergency")
                {
                    return Json(new { success = false, message = "Only pending or emergency requests can be cancelled." });
                }

                var cancelledStatus = await GetStatusTypeByNameAsync("cancelled");
                leaveRequest.StatusId = cancelledStatus.StatusId;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Leave request cancelled successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while cancelling the request.", error = ex.Message });
            }
        }

        [DynamicPermissionAuthorize("leave.approve")]
        public async Task<IActionResult> LeaveApproval()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var employee = await _context.Employee
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null) return NotFound("Employee record not found");

                // Get accessible branches based on user role and permissions
                var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(user.Id);

                // Get all leave requests from accessible branches
                var allRequests = await _context.LeaveRequests
                    .Include(lr => lr.LeaveType)
                    .Include(lr => lr.Status)
                    .Include(lr => lr.Employee)
                    .Include(lr => lr.ActionPerformer)
                    .ThenInclude(ap => ap.Employee)
                    .Where(lr => accessibleBranches.Contains(lr.Employee.BranchId))
                    .OrderByDescending(lr => lr.SubmissionDate)
                    .Select(lr => new LeaveRequestViewModel(lr.LeaveType.Name, lr.LeaveReason ?? "No reason provided")
                    {
                        Id = lr.LeaveRequestId,
                        StartDate = lr.StartDate,
                        EndDate = lr.EndDate,
                        Status = lr.Status.StatusName,
                        SubmittedOn = lr.SubmissionDate,
                        AttachmentFileName = lr.AttachmentPath,
                        RejectionReason = lr.RejectedReason,
                        EmployeeName = $"{lr.Employee.FirstName} {lr.Employee.LastName}",
                        EmployeeEmail = lr.Employee.Email,
                        ActionPerformedBy = lr.ActionPerformer != null && lr.ActionPerformer.Employee != null 
                            ? $"{lr.ActionPerformer.Employee.FirstName} {lr.ActionPerformer.Employee.LastName}"
                            : null,
                        ActionPerformedOn = lr.ActionPerformedOn
                    })
                    .ToListAsync();

                // Separate pending requests for the approval interface
                var pendingRequests = allRequests.Where(r => r.Status == "pending" || r.Status == "emergency").ToList();
                var groupedRequests = pendingRequests
                    .GroupBy(r => r.SubmittedOn.Date)
                    .OrderByDescending(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.ToList());

                ViewBag.PendingRequests = allRequests;
                ViewBag.GroupedRequests = groupedRequests;
            return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in LeaveApproval: {ex.Message}");
                throw;
            }
        }

        [DynamicPermissionAuthorize("leave.data")]
        public async Task<IActionResult> LeaveData(string search = "", string leaveType = "", string status = "", DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var employee = await _context.Employee
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null) return NotFound("Employee record not found");

                // Get accessible branches based on user role and permissions
                var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(user.Id);

                // Build the query
                var query = _context.LeaveRequests
                    .Include(lr => lr.LeaveType)
                    .Include(lr => lr.Status)
                    .Include(lr => lr.Employee)
                    .Include(lr => lr.ActionPerformer)
                    .ThenInclude(ap => ap.Employee)
                    .Where(lr => accessibleBranches.Contains(lr.Employee.BranchId));

                // Apply filters
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(lr => 
                        lr.Employee.FirstName.Contains(search) ||
                        lr.Employee.LastName.Contains(search) ||
                        lr.Employee.Email.Contains(search) ||
                        lr.LeaveType.Name.Contains(search) ||
                        lr.LeaveReason.Contains(search));
                }

                if (!string.IsNullOrEmpty(leaveType))
                {
                    query = query.Where(lr => lr.LeaveType.Name == leaveType);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(lr => lr.Status.StatusName == status);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(lr => lr.StartDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(lr => lr.EndDate <= endDate.Value);
                }

                // Get filtered results
                var leaveRequests = await query
                    .OrderByDescending(lr => lr.SubmissionDate)
                    .Select(lr => new LeaveDataViewModel
                    {
                        Id = lr.LeaveRequestId,
                        EmployeeName = $"{lr.Employee.FirstName} {lr.Employee.LastName}",
                        EmployeeEmail = lr.Employee.Email,
                        LeaveType = lr.LeaveType.Name,
                        StartDate = lr.StartDate,
                        EndDate = lr.EndDate,
                        Duration = (lr.EndDate - lr.StartDate).Days + 1,
                        Status = lr.Status.StatusName,
                        Reason = lr.LeaveReason ?? "No reason provided",
                        SubmittedOn = lr.SubmissionDate,
                        AttachmentFileName = lr.AttachmentPath,
                        RejectionReason = lr.RejectedReason,
                        // Action tracking information
                        ActionPerformedBy = lr.ActionPerformer != null && lr.ActionPerformer.Employee != null 
                            ? $"{lr.ActionPerformer.Employee.FirstName} {lr.ActionPerformer.Employee.LastName}"
                            : null,
                        ActionPerformedOn = lr.ActionPerformedOn
                    })
                    .ToListAsync();

                // Get distinct leave types and statuses for filter dropdowns
                var leaveTypes = await _context.LeaveTypes
                    .Where(lt => accessibleBranches.Contains(lt.BranchId) && lt.IsActive)
                    .Select(lt => lt.Name)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToListAsync();

                var statuses = await _context.StatusTypes
                    .Select(st => st.StatusName)
                    .OrderBy(name => name)
                    .ToListAsync();

                ViewBag.LeaveRequests = leaveRequests;
                ViewBag.LeaveTypes = leaveTypes;
                ViewBag.Statuses = statuses;
                ViewBag.Search = search;
                ViewBag.SelectedLeaveType = leaveType;
                ViewBag.SelectedStatus = status;
                ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
                ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in LeaveData: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Exports filtered leave data to Excel format (.xlsx)
        /// Uses ClosedXML library (MIT licensed) - completely free for all use cases
        /// </summary>
        [DynamicPermissionAuthorize("leave.data")]
        public async Task<IActionResult> ExportToExcel(string search = "", string leaveType = "", string status = "", DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var employee = await _context.Employee
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null) return NotFound("Employee record not found");

                // Get accessible branches based on user role and permissions
                var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(user.Id);

                // Build the query with same logic as LeaveData action
                var query = _context.LeaveRequests
                    .Include(lr => lr.LeaveType)
                    .Include(lr => lr.Status)
                    .Include(lr => lr.Employee)
                    .Include(lr => lr.ActionPerformer)
                    .ThenInclude(ap => ap.Employee)
                    .Where(lr => accessibleBranches.Contains(lr.Employee.BranchId));

                // Apply same filters as LeaveData action
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(lr => 
                        lr.Employee.FirstName.Contains(search) ||
                        lr.Employee.LastName.Contains(search) ||
                        lr.Employee.Email.Contains(search) ||
                        lr.LeaveType.Name.Contains(search) ||
                        lr.LeaveReason.Contains(search));
                }

                if (!string.IsNullOrEmpty(leaveType))
                {
                    query = query.Where(lr => lr.LeaveType.Name == leaveType);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(lr => lr.Status.StatusName == status);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(lr => lr.StartDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(lr => lr.EndDate <= endDate.Value);
                }

                // Get filtered results
                var leaveRequests = await query
                    .OrderByDescending(lr => lr.SubmissionDate)
                    .Select(lr => new LeaveDataViewModel
                    {
                        Id = lr.LeaveRequestId,
                        EmployeeName = $"{lr.Employee.FirstName} {lr.Employee.LastName}",
                        EmployeeEmail = lr.Employee.Email,
                        LeaveType = lr.LeaveType.Name,
                        StartDate = lr.StartDate,
                        EndDate = lr.EndDate,
                        Duration = (lr.EndDate - lr.StartDate).Days + 1,
                        Status = lr.Status.StatusName,
                        Reason = lr.LeaveReason ?? "No reason provided",
                        SubmittedOn = lr.SubmissionDate,
                        AttachmentFileName = lr.AttachmentPath,
                        RejectionReason = lr.RejectedReason,
                        // Action tracking information
                        ActionPerformedBy = lr.ActionPerformer != null && lr.ActionPerformer.Employee != null 
                            ? $"{lr.ActionPerformer.Employee.FirstName} {lr.ActionPerformer.Employee.LastName}"
                            : null,
                        ActionPerformedOn = lr.ActionPerformedOn
                    })
                    .ToListAsync();

                // Create Excel file using ClosedXML (completely free MIT license)
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Leave Data");

                // Add headers with styling including action tracking
                var headers = new[]
                {
                    "Employee Name", "Email", "Leave Type", "Start Date", "End Date",
                    "Duration (Days)", "Status", "Reason", "Submitted On", "Has Attachment", 
                    "Rejection Reason", "Action Performed By", "Action Date"
                };

                for (int col = 1; col <= headers.Length; col++)
                {
                    var headerCell = worksheet.Cell(1, col);
                    headerCell.Value = headers[col - 1];
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // Add data rows
                for (int i = 0; i < leaveRequests.Count; i++)
                {
                    var row = i + 2; // Start from row 2 (after header)
                    var request = leaveRequests[i];

                    worksheet.Cell(row, 1).Value = request.EmployeeName;
                    worksheet.Cell(row, 2).Value = request.EmployeeEmail;
                    worksheet.Cell(row, 3).Value = request.LeaveType;
                    worksheet.Cell(row, 4).Value = request.StartDate.ToString("yyyy-MM-dd");
                    worksheet.Cell(row, 5).Value = request.EndDate.ToString("yyyy-MM-dd");
                    worksheet.Cell(row, 6).Value = request.Duration;
                    worksheet.Cell(row, 7).Value = request.Status;
                    worksheet.Cell(row, 8).Value = request.Reason;
                    worksheet.Cell(row, 9).Value = request.SubmittedOn.ToString("yyyy-MM-dd HH:mm");
                    worksheet.Cell(row, 10).Value = !string.IsNullOrEmpty(request.AttachmentFileName) ? "Yes" : "No";
                    worksheet.Cell(row, 11).Value = request.RejectionReason ?? "";
                    // Add action tracking columns
                    worksheet.Cell(row, 12).Value = request.ActionPerformedBy ?? "";
                    worksheet.Cell(row, 13).Value = request.ActionPerformedOn?.ToString("yyyy-MM-dd HH:mm") ?? "";
                }

                // Auto-fit columns for better appearance
                worksheet.ColumnsUsed().AdjustToContents();

                // Generate file name with timestamp
                var fileName = $"LeaveData_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                
                _logger.LogInformation($"Exporting {leaveRequests.Count} leave records to Excel for user {user.Email}");

                // Convert to byte array and return file
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var fileBytes = stream.ToArray();
                
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ExportToExcel: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Exports filtered leave data to CSV format
        /// No additional packages required - uses built-in .NET functionality
        /// </summary>
        [DynamicPermissionAuthorize("leave.data")]
        public async Task<IActionResult> ExportToCsv(string search = "", string leaveType = "", string status = "", DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var employee = await _context.Employee
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null) return NotFound("Employee record not found");

                // Get accessible branches based on user role and permissions
                var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(user.Id);

                // Build the query with same logic as LeaveData action
                var query = _context.LeaveRequests
                    .Include(lr => lr.LeaveType)
                    .Include(lr => lr.Status)
                    .Include(lr => lr.Employee)
                    .Include(lr => lr.ActionPerformer)
                    .ThenInclude(ap => ap.Employee)
                    .Where(lr => accessibleBranches.Contains(lr.Employee.BranchId));

                // Apply same filters as LeaveData action
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(lr => 
                        lr.Employee.FirstName.Contains(search) ||
                        lr.Employee.LastName.Contains(search) ||
                        lr.Employee.Email.Contains(search) ||
                        lr.LeaveType.Name.Contains(search) ||
                        lr.LeaveReason.Contains(search));
                }

                if (!string.IsNullOrEmpty(leaveType))
                {
                    query = query.Where(lr => lr.LeaveType.Name == leaveType);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(lr => lr.Status.StatusName == status);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(lr => lr.StartDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(lr => lr.EndDate <= endDate.Value);
                }

                // Get filtered results
                var leaveRequests = await query
                    .OrderByDescending(lr => lr.SubmissionDate)
                    .Select(lr => new LeaveDataViewModel
                    {
                        Id = lr.LeaveRequestId,
                        EmployeeName = $"{lr.Employee.FirstName} {lr.Employee.LastName}",
                        EmployeeEmail = lr.Employee.Email,
                        LeaveType = lr.LeaveType.Name,
                        StartDate = lr.StartDate,
                        EndDate = lr.EndDate,
                        Duration = (lr.EndDate - lr.StartDate).Days + 1,
                        Status = lr.Status.StatusName,
                        Reason = lr.LeaveReason ?? "No reason provided",
                        SubmittedOn = lr.SubmissionDate,
                        AttachmentFileName = lr.AttachmentPath,
                        RejectionReason = lr.RejectedReason,
                        // Action tracking information
                        ActionPerformedBy = lr.ActionPerformer != null && lr.ActionPerformer.Employee != null 
                            ? $"{lr.ActionPerformer.Employee.FirstName} {lr.ActionPerformer.Employee.LastName}"
                            : null,
                        ActionPerformedOn = lr.ActionPerformedOn
                    })
                    .ToListAsync();

                // Create CSV content with action tracking columns
                var csv = new StringBuilder();
                
                // Add header with action tracking
                csv.AppendLine("Employee Name,Email,Leave Type,Start Date,End Date,Duration (Days),Status,Reason,Submitted On,Has Attachment,Rejection Reason,Action Performed By,Action Date");

                // Add data rows
                foreach (var request in leaveRequests)
                {
                    csv.AppendLine($"{EscapeCsvField(request.EmployeeName)}," +
                                 $"{EscapeCsvField(request.EmployeeEmail)}," +
                                 $"{EscapeCsvField(request.LeaveType)}," +
                                 $"{EscapeCsvField(request.StartDate.ToString("yyyy-MM-dd"))}," +
                                 $"{EscapeCsvField(request.EndDate.ToString("yyyy-MM-dd"))}," +
                                 $"{request.Duration}," +
                                 $"{EscapeCsvField(request.Status)}," +
                                 $"{EscapeCsvField(request.Reason)}," +
                                 $"{EscapeCsvField(request.SubmittedOn.ToString("yyyy-MM-dd HH:mm"))}," +
                                 $"{(!string.IsNullOrEmpty(request.AttachmentFileName) ? "Yes" : "No")}," +
                                 $"{EscapeCsvField(request.RejectionReason ?? "")}," +
                                 $"{EscapeCsvField(request.ActionPerformedBy ?? "")}," +
                                 $"{EscapeCsvField(request.ActionPerformedOn?.ToString("yyyy-MM-dd HH:mm") ?? "")}");
                }

                // Generate file name with timestamp
                var fileName = $"LeaveData_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                
                _logger.LogInformation($"Exporting {leaveRequests.Count} leave records to CSV for user {user.Email}");

                // Return CSV file
                var fileBytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(fileBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ExportToCsv: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Helper method to escape CSV fields that contain commas, quotes, or newlines
        /// </summary>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [DynamicPermissionAuthorize("leave.approve")]
        public async Task<IActionResult> Approve([FromBody] int id)
        {
            try
            {
                _logger.LogInformation($"Attempting to approve leave request with ID: {id}");
                
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });
                
                var leaveRequest = await _context.LeaveRequests
                    .Include(lr => lr.Employee)
                    .Include(lr => lr.LeaveType)
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == id);

                if (leaveRequest == null)
                {
                    _logger.LogWarning($"Leave request with ID {id} not found");
                    return Json(new { success = false, message = "Leave request not found." });
                }

                // Check if current user can access the employee's branch
                if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, leaveRequest.Employee.BranchId))
                {
                    return Json(new { success = false, message = "You don't have permission to approve leave requests for this branch." });
                }

                var approvedStatus = await GetStatusTypeByNameAsync("approved");
                leaveRequest.StatusId = approvedStatus.StatusId;
                
                // Track who approved and when
                leaveRequest.ActionPerformedBy = currentUser.Id;
                leaveRequest.ActionPerformedOn = DateTime.Now;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully approved leave request with ID: {id} by user {currentUser.Id}");
                return Json(new { success = true, message = "Leave request approved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error approving leave request: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while approving the request." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [DynamicPermissionAuthorize("leave.reject")]
        public async Task<IActionResult> Reject([FromBody] RejectLeaveRequestModel model)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Json(new { success = false, message = "User not authenticated" });

                var leaveRequest = await _context.LeaveRequests
                    .Include(lr => lr.Employee)
                    .Include(lr => lr.LeaveType)
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == model.Id);

                if (leaveRequest == null)
                {
                    return Json(new { success = false, message = "Leave request not found." });
                }

                // Check if current user can access the employee's branch
                if (!await _branchAccessService.CanAccessBranchAsync(currentUser.Id, leaveRequest.Employee.BranchId))
                {
                    return Json(new { success = false, message = "You don't have permission to reject leave requests for this branch." });
                }

                if (string.IsNullOrWhiteSpace(model.RejectionReason))
                {
                    return Json(new { success = false, message = "Rejection reason is required." });
                }

                var rejectedStatus = await GetStatusTypeByNameAsync("rejected");
                leaveRequest.StatusId = rejectedStatus.StatusId;
                leaveRequest.RejectedReason = model.RejectionReason;
                
                // Track who rejected and when
                leaveRequest.ActionPerformedBy = currentUser.Id;
                leaveRequest.ActionPerformedOn = DateTime.Now;
                
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Leave request rejected successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error rejecting leave request: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while rejecting the request." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            try
            {
                _logger.LogInformation($"Attempting to download attachment for leave request ID: {id}");

                var user = await _userManager.GetUserAsync(User);
                if (user == null) 
                {
                    _logger.LogWarning("User not authenticated for download request");
                    return Challenge();
                }

                var employee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null) 
                {
                    _logger.LogWarning($"Employee record not found for user {user.Id}");
                    return NotFound("Employee record not found");
                }

                var leaveRequest = await _context.LeaveRequests
                    .Include(lr => lr.Employee)
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == id);

                if (leaveRequest == null)
                {
                    _logger.LogWarning($"Leave request with ID {id} not found");
                    return NotFound("Leave request not found");
                }

                if (string.IsNullOrEmpty(leaveRequest.AttachmentPath))
                {
                    _logger.LogWarning($"No attachment found for leave request ID {id}");
                    return NotFound("No attachment found for this leave request");
                }

                _logger.LogInformation($"Found leave request {id} with attachment: {leaveRequest.AttachmentPath}");

                // Check if user can access this attachment
                bool canAccess = false;
                
                // Employee can access their own attachments
                if (leaveRequest.EmployeeId == employee.EmployeeId)
                {
                    canAccess = true;
                    _logger.LogInformation($"User {user.Id} accessing their own attachment");
                }
                else
                {
                    // Get permission service to check user permissions properly
                    var permissionService = HttpContext.RequestServices.GetRequiredService<IDynamicPermissionService>();
                    
                    // Check if user has permission to view leave requests or approve them
                    var hasViewPermission = await permissionService.HasPermissionAsync(user.Id, "leave.view");
                    var hasApprovePermission = await permissionService.HasPermissionAsync(user.Id, "leave.approve");
                    var hasDataPermission = await permissionService.HasPermissionAsync(user.Id, "leave.data");
                    
                    if (hasViewPermission || hasApprovePermission || hasDataPermission)
                    {
                        var accessibleBranches = await _branchAccessService.GetAccessibleBranchIdsAsync(user.Id);
                        canAccess = accessibleBranches.Contains(leaveRequest.Employee.BranchId);
                        _logger.LogInformation($"User {user.Id} has permission to view leaves. Can access branch {leaveRequest.Employee.BranchId}: {canAccess}");
                    }
                    else
                    {
                        _logger.LogInformation($"User {user.Id} does not have leave.view, leave.approve, or leave.data permissions");
                    }
                }

                if (!canAccess)
                {
                    _logger.LogWarning($"User {user.Id} does not have permission to access attachment for leave request {id}");
                    return StatusCode(403, "You don't have permission to access this attachment");
                }

                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "leave-attachments", leaveRequest.AttachmentPath);
                _logger.LogInformation($"Looking for file at path: {filePath}");

                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning($"File not found at path: {filePath}");
                    
                    // Let's also check if the file exists with different casing or in alternative locations
                    var directory = Path.GetDirectoryName(filePath);
                    if (Directory.Exists(directory))
                    {
                        var filesInDir = Directory.GetFiles(directory);
                        _logger.LogInformation($"Files in directory: {string.Join(", ", filesInDir.Select(f => Path.GetFileName(f)))}");
                        
                        // Try to find a file that matches case-insensitively
                        var requestedFileName = Path.GetFileName(leaveRequest.AttachmentPath);
                        var matchingFile = filesInDir.FirstOrDefault(f => 
                            string.Equals(Path.GetFileName(f), requestedFileName, StringComparison.OrdinalIgnoreCase));
                        
                        if (matchingFile != null)
                        {
                            _logger.LogInformation($"Found file with different casing: {matchingFile}");
                            filePath = matchingFile;
                        }
                    }
                }

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("File not found on server. The attachment may have been moved or deleted.");
                }

                var fileName = Path.GetFileName(leaveRequest.AttachmentPath);
                var contentType = GetContentType(fileName);
                
                _logger.LogInformation($"Successfully serving attachment: {fileName} (Content-Type: {contentType}) for leave request {id}");
                return PhysicalFile(filePath, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading attachment for leave request {id}: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, "An error occurred while downloading the file");
            }
        }

        [HttpGet]
        public async Task<IActionResult> DebugAttachment(int id)
        {
            try
            {
                var leaveRequest = await _context.LeaveRequests
                    .Include(lr => lr.Employee)
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == id);

                if (leaveRequest == null)
                {
                    return Json(new { error = "Leave request not found", id = id });
                }

                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "leave-attachments", leaveRequest.AttachmentPath ?? "null");
                var fileExists = System.IO.File.Exists(filePath);
                
                var directoryPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "leave-attachments");
                var directoryExists = Directory.Exists(directoryPath);
                var filesInDirectory = directoryExists ? Directory.GetFiles(directoryPath).Select(f => Path.GetFileName(f)).ToList() : new List<string>();

                return Json(new {
                    leaveRequestId = id,
                    attachmentPath = leaveRequest.AttachmentPath,
                    fullFilePath = filePath,
                    fileExists = fileExists,
                    directoryExists = directoryExists,
                    filesInDirectory = filesInDirectory,
                    employeeId = leaveRequest.EmployeeId,
                    employeeName = $"{leaveRequest.Employee.FirstName} {leaveRequest.Employee.LastName}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        public class RejectLeaveRequestModel
        {
            public int Id { get; set; }
            public string RejectionReason { get; set; }
        }
    }

    public class LeaveRequestViewModel
    {
        public LeaveRequestViewModel() { }

        public LeaveRequestViewModel(string leaveType, string reason)
        {
            LeaveType = leaveType;
            Reason = reason;
        }

        public int Id { get; set; }

        [Required(ErrorMessage = "Please select a leave type")]
        public string LeaveType { get; set; }

        [Required(ErrorMessage = "Please specify if this is an emergency request")]
        public string Emergency { get; set; } = "no";

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Required(ErrorMessage = "Please provide a reason for your leave request")]
        [StringLength(500, ErrorMessage = "Reason cannot be longer than 500 characters")]
        public string Reason { get; set; }

        public string Status { get; set; } = "pending";

        public string? RejectionReason { get; set; }

        public string? AttachmentFileName { get; set; }

        public DateTime SubmittedOn { get; set; } = DateTime.Now;

        // Action tracking fields
        public string? ActionPerformedBy { get; set; } // Name of the person who approved/rejected
        public DateTime? ActionPerformedOn { get; set; } // When the approval/rejection was done

        // Make these fields optional since they're populated server-side
        public string? EmployeeName { get; set; }
        public string? EmployeeEmail { get; set; }
        public int Days { get; set; }
    }

    public class LeaveBalanceViewModel
    {
        public LeaveBalanceViewModel(string type)
        {
            Type = type;
        }

        public string Type { get; init; }
        public int Remaining { get; set; }
        public int Used { get; set; }
        public int Total { get; set; }
    }

    public class LeaveTypeViewModel
    {
        public int LeaveTypeId { get; set; }
        public string Name { get; set; }
        public int DefaultDays { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public int BranchId { get; set; }
    }

    public class LeaveDataViewModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeEmail { get; set; }
        public string LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Duration { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public DateTime SubmittedOn { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? RejectionReason { get; set; }
        
        // Action tracking fields
        public string? ActionPerformedBy { get; set; } // Name of the person who approved/rejected
        public DateTime? ActionPerformedOn { get; set; } // When the approval/rejection was done
    }
}
