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
                        RejectionReason = lr.RejectedReason
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
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return Challenge();

                var accessibleBranchIds = await _branchAccessService.GetAccessibleBranchIdsAsync(currentUser.Id);

                var pendingStatusId = (await GetStatusTypeByNameAsync("pending")).StatusId;
                var emergencyStatusId = (await GetStatusTypeByNameAsync("emergency")).StatusId;

                var allRequests = await _context.LeaveRequests
                .Include(lr => lr.LeaveType)
                .Include(lr => lr.Employee)
                    .ThenInclude(e => e.Branch)
                    .Include(lr => lr.Status)
                    .Where(lr => accessibleBranchIds.Contains(lr.Employee.BranchId)) // Filter by accessible branches
                    .OrderByDescending(lr => lr.SubmissionDate)
                    .Select(lr => new LeaveRequestViewModel(lr.LeaveType.Name, lr.LeaveReason ?? "No reason provided")
                {
                    Id = lr.LeaveRequestId,
                    StartDate = lr.StartDate,
                    EndDate = lr.EndDate,
                        Status = lr.Status.StatusName,
                    SubmittedOn = lr.SubmissionDate,
                        Emergency = lr.Status.StatusName == "emergency" ? "yes" : "no",
                        AttachmentFileName = lr.AttachmentPath,
                        EmployeeName = lr.Employee.FirstName + " " + lr.Employee.LastName,
                        EmployeeEmail = lr.Employee.Email,
                        LeaveType = lr.LeaveType.Name,
                        Reason = lr.LeaveReason,
                        Days = (lr.EndDate - lr.StartDate).Days + 1,
                        RejectionReason = lr.RejectedReason
                })
                .ToListAsync();

                // Group requests by submission date
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
            await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully approved leave request with ID: {id}");
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
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var employee = await _context.Employee
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);
                if (employee == null) return NotFound("Employee record not found");

                var leaveRequest = await _context.LeaveRequests
                    .FirstOrDefaultAsync(lr => lr.LeaveRequestId == id && 
                        (lr.EmployeeId == employee.EmployeeId || User.IsInRole("Admin") || User.IsInRole("Manager")));

                if (leaveRequest == null || string.IsNullOrEmpty(leaveRequest.AttachmentPath))
                {
                    return NotFound("Attachment not found");
                }

                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "leave-attachments", leaveRequest.AttachmentPath);
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("File not found");
                }

                var fileName = Path.GetFileName(leaveRequest.AttachmentPath);
                var contentType = "application/octet-stream";
                return PhysicalFile(filePath, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading attachment: {ex.Message}");
                return StatusCode(500, "An error occurred while downloading the file");
            }
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
}
