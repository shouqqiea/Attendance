using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AttendEase.Controllers
{
    // Simple model for demonstration. In production, use a separate file and add more validation as needed.
    public class LeaveRequestViewModel
    {
        public int Id { get; set; }

        [Required]
        public string LeaveType { get; set; }

        [Required]
        public string Emergency { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Required]
        public string Reason { get; set; }

        public string Status { get; set; } = "pending"; // pending, approved, rejected, emergency

        public string RejectionReason { get; set; }

        public string AttachmentFileName { get; set; }

        public DateTime SubmittedOn { get; set; } = DateTime.Now;
    }

    public class LeaveBalanceViewModel
    {
        public string Type { get; set; }
        public int Remaining { get; set; }
        public int Used { get; set; }
        public int Total { get; set; }
    }

    public class LeaveManagementController : Controller
    {
        // Simulated in-memory data store for demonstration
        private static List<LeaveRequestViewModel> _leaveRequests = new List<LeaveRequestViewModel>
        {
            new LeaveRequestViewModel
            {
                Id = 1,
                LeaveType = "vacation",
                Emergency = "no",
                StartDate = new DateTime(2023, 5, 22),
                EndDate = new DateTime(2023, 5, 26),
                Reason = "Family vacation planned months in advance. All project deliverables will be completed before departure.",
                Status = "pending",
                SubmittedOn = new DateTime(2023, 5, 10)
            },
            new LeaveRequestViewModel
            {
                Id = 2,
                LeaveType = "sick",
                Emergency = "no",
                StartDate = new DateTime(2023, 4, 15),
                EndDate = new DateTime(2023, 4, 15),
                Reason = "Not feeling well, need to rest and recover. Doctor's note attached.",
                Status = "approved",
                SubmittedOn = new DateTime(2023, 4, 14)
            },
            new LeaveRequestViewModel
            {
                Id = 3,
                LeaveType = "personal",
                Emergency = "no",
                StartDate = new DateTime(2023, 3, 10),
                EndDate = new DateTime(2023, 3, 11),
                Reason = "Need to attend a personal event.",
                Status = "rejected",
                RejectionReason = "Critical project deadline on March 11. Please reschedule your leave after the project delivery.",
                SubmittedOn = new DateTime(2023, 3, 8)
            }
        };

        private static List<LeaveBalanceViewModel> _leaveBalances = new List<LeaveBalanceViewModel>
        {
            new LeaveBalanceViewModel { Type = "Vacation Leave", Remaining = 12, Used = 8, Total = 20 },
            new LeaveBalanceViewModel { Type = "Sick Leave", Remaining = 7, Used = 3, Total = 10 },
            new LeaveBalanceViewModel { Type = "Personal Leave", Remaining = 2, Used = 3, Total = 5 }
        };

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult LeaveRequest()
        {
            ViewBag.LeaveRequests = _leaveRequests;
            ViewBag.LeaveBalances = _leaveBalances;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LeaveRequest(LeaveRequestViewModel model, IFormFile attachment)
        {
            if (ModelState.IsValid)
            {
                model.Id = _leaveRequests.Count + 1;
                model.Status = model.Emergency == "yes" ? "emergency" : "pending";
                model.SubmittedOn = DateTime.Now;

                if (attachment != null && attachment.Length > 0)
                {
                    // For demonstration, just store the file name. In production, save the file securely.
                    model.AttachmentFileName = attachment.FileName;
                }

                _leaveRequests.Add(model);
                TempData["Success"] = "Leave request submitted successfully.";
                return RedirectToAction(nameof(LeaveRequest));
            }

            ViewBag.LeaveRequests = _leaveRequests;
            ViewBag.LeaveBalances = _leaveBalances;
            return View(model);
        }

        public IActionResult LeaveApproval()
        {
            // Show all pending and emergency requests for approval
            var pending = _leaveRequests.FindAll(r => r.Status == "pending" || r.Status == "emergency");

            // If no pending requests, add a dummy leave item for UI/JS testing
            if (pending.Count == 0)
            {
                pending.Add(new LeaveRequestViewModel
                {
                    Id = 999,
                    LeaveType = "vacation",
                    Emergency = "no",
                    StartDate = new DateTime(2023, 7, 1),
                    EndDate = new DateTime(2023, 7, 3),
                    Reason = "Demo leave request for testing approval and rejection workflow.",
                    Status = "pending",
                    SubmittedOn = new DateTime(2023, 6, 25)
                });
            }

            ViewBag.PendingRequests = pending;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int id)
        {
            var req = _leaveRequests.Find(r => r.Id == id);
            if (req != null)
            {
                req.Status = "approved";
            }
            return RedirectToAction(nameof(LeaveApproval));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(int id, string rejectionReason)
        {
            var req = _leaveRequests.Find(r => r.Id == id);
            if (req != null)
            {
                req.Status = "rejected";
                req.RejectionReason = rejectionReason;
            }
            return RedirectToAction(nameof(LeaveApproval));
        }
    }
}
