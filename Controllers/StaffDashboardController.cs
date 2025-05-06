using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using UnivMate.Data;
using UnivMate.ViewModels;
using System;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Security.Claims;
using UnivMate.Models;
using Microsoft.Extensions.Logging;

namespace UnivMate.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class StaffDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StaffDashboardController> _logger;

        public StaffDashboardController(ApplicationDbContext context, ILogger<StaffDashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string status = "all", int page = 1, int pageSize = 10)
        {
            var reportsQuery = _context.Reports
                .Include(r => r.User)
                .Include(r => r.AssignedTo)
                .Include(r => r.ResolvedBy)
                .OrderByDescending(r => r.SubmittedAt);

            if (!string.IsNullOrEmpty(status) && status.ToLower() != "all")
            {
                reportsQuery = (IOrderedQueryable<Reports>)reportsQuery
                    .Where(r => r.Status.ToLower() == status.ToLower());
            }

            var totalReports = await reportsQuery.CountAsync();
            var reports = await reportsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new StaffDashboardViewModel
            {
                Reports = reports,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalReports / (double)pageSize),
                CurrentStatus = status,
                RecentUsers = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(5)
                    .ToListAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                TotalStudents = await _context.Users
                    .Where(u => u.Role.ToLower() == "student")
                    .CountAsync(),
                TotalProfessors = await _context.Users
                    .Where(u => u.Role.ToLower() == "professor")
                    .CountAsync(),
                PendingReports = await _context.Reports
                    .Where(r => r.Status == "Pending")
                    .CountAsync()
            };

            return View(model);
        }

        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> StaffDashboard()
        {
            try
            {
                var model = new StaffDashboardViewModel
                {
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalStudents = await _context.Users
                        .Where(u => u.Role.ToLower() == "student")
                        .CountAsync(),
                    TotalProfessors = await _context.Users
                        .Where(u => u.Role.ToLower() == "professor")
                        .CountAsync(),
                    PendingReports = await _context.Reports
                        .Where(r => r.Status == "Pending")
                        .CountAsync(),
                    Reports = await _context.Reports
                        .Include(r => r.User)
                        .OrderByDescending(r => r.SubmittedAt)
                        .Take(10)
                        .ToListAsync(),
                    RecentUsers = await _context.Users
                        .OrderByDescending(u => u.CreatedAt)
                        .Take(5)
                        .ToListAsync()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading StaffDashboard");
                TempData["ErrorMessage"] = "Error loading dashboard data";
                return View(new StaffDashboardViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardCounts()
        {
            try
            {
                var counts = new
                {
                    totalUsers = await _context.Users.CountAsync(),
                    totalStudents = await _context.Users
                        .Where(u => u.Role.ToLower() == "student")
                        .CountAsync(),
                    totalProfessors = await _context.Users
                        .Where(u => u.Role.ToLower() == "professor")
                        .CountAsync(),
                    pendingReports = await _context.Reports
                        .Where(r => r.Status == "Pending")
                        .CountAsync()
                };
                return Json(counts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard counts");
                return StatusCode(500, new { error = "Error retrieving dashboard counts" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> QuickAction(int reportId, string action)
        {
            try
            {
                var report = await _context.Reports.FindAsync(reportId);
                if (report == null)
                {
                    return NotFound();
                }

                var staffId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                switch (action.ToLower())
                {
                    case "accept":
                        report.Status = "In Progress";
                        report.AssignedToId = staffId;
                        report.AssignedAt = DateTime.UtcNow;
                        break;

                    case "reject":
                        report.Status = "Rejected";
                        report.ResolvedById = staffId;
                        report.ResolvedAt = DateTime.UtcNow;
                        break;

                    case "complete":
                        report.Status = "Completed";
                        report.ResolvedById = staffId;
                        report.ResolvedAt = DateTime.UtcNow;
                        break;

                    default:
                        return BadRequest("Invalid action specified");
                }

                _context.Update(report);
                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Report #{reportId} updated successfully";
                return RedirectToAction("Index"); // Changed from StaffDashboard to Index
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing quick action on report {reportId}");
                TempData["ErrorMessage"] = "Error processing report action";
                return RedirectToAction("Index"); // Changed from StaffDashboard to Index
            }
        

        }

        [HttpGet]
        public async Task<IActionResult> ViewReport(int id)
        {
            try
            {
                var report = await _context.Reports
                    .Include(r => r.User)
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ResolvedBy)
                    .Include(r => r.StatusHistory)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (report == null)
                {
                    return NotFound();
                }

                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error viewing report {id}");
                TempData["ErrorMessage"] = "Error loading report details";
                return RedirectToAction("StaffDashboard");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateReportStatus(int reportId, string status, string resolutionNotes)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(status))
                {
                    ModelState.AddModelError("Status", "Status is required.");
                    return BadRequest(ModelState);
                }

                var report = await _context.Reports
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null) return NotFound();

                var staffId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var previousStatus = report.Status;
                report.Status = status;
                report.ResolutionNotes = resolutionNotes;

                switch (status)
                {
                    case "In Progress":
                        report.AssignedToId = staffId;
                        report.AssignedAt = DateTime.UtcNow;
                        break;

                    case "Completed":
                    case "Rejected":
                        report.ResolvedById = staffId;
                        report.ResolvedAt = DateTime.UtcNow;
                        break;
                }

                _context.Update(report);
                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Report #{reportId} status updated to {status}";
                return RedirectToAction("ViewReport", new { id = reportId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for report {reportId}");
                TempData["ErrorMessage"] = "Error updating report status";
                return RedirectToAction("ViewReport", new { id = reportId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteReport(int id)
        {
            try
            {
                var report = await _context.Reports.FindAsync(id);
                if (report == null)
                {
                    return NotFound();
                }

                if (!string.IsNullOrEmpty(report.ImagePath))
                {
                    var imagePath = Path.Combine("wwwroot", report.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                _context.Reports.Remove(report);
                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Report #{id} deleted successfully";
                return RedirectToAction("StaffDashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting report {id}");
                TempData["ErrorMessage"] = "Error deleting report";
                return RedirectToAction("StaffDashboard");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentReports()
        {
            try
            {
                var reports = await _context.Reports
                    .Include(r => r.User)
                    .OrderByDescending(r => r.SubmittedAt)
                    .Take(5)
                    .Select(r => new {
                        r.Id,
                        r.Title,
                        r.Status,
                        SubmittedAt = r.SubmittedAt.ToString("MMM dd, yyyy"),
                        UserName = r.User.FirstName + " " + r.User.LastName
                    })
                    .ToListAsync();

                return PartialView("_RecentReportsPartial", reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent reports");
                return StatusCode(500, "Error loading recent reports");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessReport(
            [FromForm] int reportId,
            [FromForm(Name = "action")] string action,
            [FromForm] string resolutionNotes)
        {
            try
            {
                _logger.LogInformation($"Processing report {reportId}, action: {action}");

                var report = await _context.Reports
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ResolvedBy)
                    .Include(r => r.StatusHistory)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null)
                {
                    _logger.LogWarning($"Report {reportId} not found");
                    return NotFound();
                }

                var currentUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == User.Identity.Name);

                if (currentUser == null)
                {
                    _logger.LogWarning("Current user not found");
                    return Forbid();
                }

                var oldStatus = report.Status;
                string newStatus;
                string actionMessage;

                switch (action.ToLower())
                {
                    case "accept":
                        newStatus = "In Progress";
                        report.AssignedTo = currentUser;
                        report.AssignedAt = DateTime.UtcNow;
                        actionMessage = "accepted";
                        break;

                    case "reject":
                        newStatus = "Rejected";
                        report.ResolvedBy = currentUser;
                        report.ResolvedAt = DateTime.UtcNow;
                        report.ResolutionNotes = resolutionNotes;
                        actionMessage = "rejected";
                        break;

                    default:
                        _logger.LogWarning($"Invalid action: {action}");
                        return BadRequest("Invalid action");
                }

                report.Status = newStatus;
                await RecordStatusChange(report, oldStatus, newStatus,
                    $"Status changed via {action}. Notes: {resolutionNotes}");

                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Report #{reportId} has been {actionMessage} successfully";
                return RedirectToAction("ViewReport", new { id = reportId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing report {reportId}");
                TempData["ErrorMessage"] = "An error occurred while processing the report";
                return RedirectToAction("ViewReport", new { id = reportId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteReport(
           [FromForm] int reportId,
           [FromForm] string resolutionNotes)
        {
            try
            {
                var report = await _context.Reports
                    .Include(r => r.AssignedTo)
                    .Include(r => r.StatusHistory)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null)
                {
                    return NotFound();
                }

                var currentUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == User.Identity.Name);

                if (currentUser == null || report.AssignedTo?.Id != currentUser.Id)
                {
                    return Forbid();
                }

                var oldStatus = report.Status;
                report.Status = "Completed";
                report.ResolvedBy = currentUser;
                report.ResolvedAt = DateTime.UtcNow;
                report.ResolutionNotes = resolutionNotes;

                await RecordStatusChange(report, oldStatus, "Completed",
                    $"Report completed. Notes: {resolutionNotes}");

                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Report #{reportId} has been marked as completed";
                return RedirectToAction("ViewReport", new { id = reportId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error completing report {reportId}");
                TempData["ErrorMessage"] = "An error occurred while completing the report";
                return RedirectToAction("ViewReport", new { id = reportId });
            }
        }

        private async Task RecordStatusChange(Reports report, string oldStatus, string newStatus, string notes)
        {
            var history = new ReportStatusHistory
            {
                ReportId = report.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedBy = User.Identity.Name,
                ChangedAt = DateTime.UtcNow,
                Notes = notes
            };

            _context.ReportStatusHistories.Add(history);
            await _context.SaveChangesAsync();
        }
    }
}