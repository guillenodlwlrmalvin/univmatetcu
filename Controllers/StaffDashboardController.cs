
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickAction(int reportId, string action, string comment)
        {
            try
            {
                var report = await _context.Reports
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null) return NotFound();

                var staffId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var staffUser = await _context.Users.FindAsync(staffId);

                switch (action.ToLower())
                {
                    case "accept":
                        report.Status = "In Progress";
                        report.AssignedToId = staffId;
                        report.AssignedAt = DateTime.UtcNow;
                        report.AdminComment = comment;
                        await RecordStatusChange(report, "Pending", "In Progress",
                            $"Report accepted. Staff comment: {comment}");

                        // Create notification
                        await CreateNotification(report.UserId,
                            $"Your report '{report.Title}' has been accepted",
                            comment);
                        break;

                    case "reject":
                        report.Status = "Rejected";
                        report.ResolvedById = staffId;
                        report.ResolvedAt = DateTime.UtcNow;
                        report.AdminComment = comment;
                        await RecordStatusChange(report, "Pending", "Rejected",
                            $"Report rejected. Staff comment: {comment}");

                        // Create notification
                        await CreateNotification(report.UserId,
                            $"Your report '{report.Title}' has been rejected",
                            comment);
                        break;

                    case "complete":
                        // Return JSON response to trigger the modal
                        return Json(new
                        {
                            showModal = true,
                            reportId = reportId
                        });

                    default:
                        return BadRequest("Invalid action specified");
                }

                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = $"Report #{reportId} has been {action.ToLower()}ed successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing quick action on report {reportId}");
                TempData["ErrorMessage"] = "Error processing report action";
                return RedirectToAction("Index");
            }
        }

        private async Task CreateNotification(int userId, string message, string details = null, int? reportId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                ReportId = reportId,
                Message = message,
                Details = details,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        // Add this to your controller
        [Authorize]
        public async Task<IActionResult> Notifications()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
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
                    .Include(r => r.Comments)
                        .ThenInclude(c => c.Author) // Include the comment author
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
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken] // Add this attribute
        public async Task<IActionResult> AddComment(int reportId, string comment, IFormFile commentImage)
        {
            try
            {
                var report = await _context.Reports
                    .Include(r => r.User)
                    .Include(r => r.Comments)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                var staffId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var staffUser = await _context.Users.FindAsync(staffId);

                if (string.IsNullOrWhiteSpace(comment) && commentImage == null)
                {
                    return Json(new { success = false, message = "Comment or image is required" });
                }

                // Handle image upload
                string imagePath = null;
                if (commentImage != null && commentImage.Length > 0)
                {
                    // Validate file size (5MB max)
                    if (commentImage.Length > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "Image size must be less than 5MB" });
                    }

                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(commentImage.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return Json(new { success = false, message = "Only image files are allowed (JPG, PNG, GIF)" });
                    }

                    // Create uploads directory if it doesn't exist
                    var uploadsDir = Path.Combine("wwwroot", "uploads", "comments");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    // Generate unique filename
                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsDir, uniqueFileName);

                    _logger.LogInformation($"Attempting to save image: {uniqueFileName}");
                    // Save the file
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await commentImage.CopyToAsync(fileStream);
                    }

                    _logger.LogInformation($"Successfully saved image: {filePath}");
                    imagePath = $"/uploads/comments/{uniqueFileName}";
                }

                // Create new comment
                var newComment = new ReportComment
                {
                    Content = comment,
                    ImagePath = imagePath,
                    ReportId = reportId,
                    AuthorId = staffId,
                    CreatedAt = DateTime.Now
                };

                _context.ReportComments.Add(newComment);

                // Update report assignment if not already assigned
                if (report.AssignedToId == null)
                {
                    report.AssignedToId = staffId;
                    report.AssignedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    commentContent = comment,
                    imagePath = imagePath,
                    authorName = $"{staffUser.FirstName} {staffUser.LastName}",
                    authorEmail = staffUser.Email,
                    createdAt = DateTime.Now.ToString("f")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding comment to report {reportId}");
                return Json(new { success = false, message = "Error adding comment" });
            }
        }
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteReportWithDetails(
    [FromForm] int reportId,
    [FromForm] string completionNotes,
    [FromForm] IFormFile completionImage)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(completionNotes))
                {
                    return Json(new { success = false, message = "Completion notes are required" });
                }

                var report = await _context.Reports
                    .Include(r => r.User)
                    .Include(r => r.AssignedTo)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var currentUser = await _context.Users.FindAsync(currentUserId);

                // Handle image upload if present
                string imagePath = null;
                if (completionImage != null && completionImage.Length > 0)
                {
                    // Validate file size (5MB max)
                    if (completionImage.Length > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "Image size exceeds 5MB limit" });
                    }

                    // Validate file type
                    var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var extension = Path.GetExtension(completionImage.FileName).ToLowerInvariant();
                    if (!validExtensions.Contains(extension))
                    {
                        return Json(new { success = false, message = "Invalid image file type" });
                    }

                    // Save the file
                    var uploadsFolder = Path.Combine("wwwroot", "uploads", "completion");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await completionImage.CopyToAsync(fileStream);
                    }

                    imagePath = $"/uploads/completion/{uniqueFileName}";
                }

                // Update the report status
                var oldStatus = report.Status;
                report.Status = "Completed";
                report.ResolutionNotes = completionNotes;
                report.ResolvedById = currentUserId;
                report.ResolvedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(imagePath))
                {
                    report.ImagePath = imagePath;
                }

                // Record status change
                await RecordStatusChange(report, oldStatus, "Completed",
                    $"Report completed with notes: {completionNotes}");

                // Add an automatic comment about the completion
                var completionComment = new ReportComment
                {
                    ReportId = reportId,
                    AuthorId = currentUserId,
                    Content = $"Report marked as completed: {completionNotes}",
                    CreatedAt = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(imagePath))
                {
                    completionComment.ImagePath = imagePath;
                    completionComment.Content += " (see attached image)";
                }

                _context.ReportComments.Add(completionComment);

                await _context.SaveChangesAsync();

                // Create notification for the user
                await CreateNotification(
                    report.UserId,
                    $"Your report '{report.Title}' has been completed",
                    completionNotes,
                    reportId);

                return Json(new
                {
                    success = true,
                    message = "Report successfully marked as completed",
                    reportId = reportId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error completing report {reportId} with details");
                return Json(new { success = false, message = "An error occurred while completing the report" });
            }
        }
    }
}