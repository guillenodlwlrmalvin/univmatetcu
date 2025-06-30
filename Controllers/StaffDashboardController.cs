
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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting;
using DinkToPdf;
using Xceed.Words.NET;
using Xceed.Document.NET;
using System.Drawing;
using SkiaSharp;



namespace UnivMate.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class StaffDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StaffDashboardController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public StaffDashboardController(ApplicationDbContext context, ILogger<StaffDashboardController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index(string status = "all", int page = 1, int pageSize = 10)
        {
            var reportsQuery = _context.Reports
                .Include(r => r.User)
                .Include(r => r.AssignedTo)
                .Include(r => r.ResolvedBy)
                .Include(r => r.Comments)
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
                    .CountAsync(),
                UnrespondedReportsCount = await _context.Reports
                    .Include(r => r.Comments)
                    .Where(r => r.Comments.Count == 0 && r.Status != "Completed")
                    .CountAsync(),
                UnrespondedReports = await _context.Reports
                    .Include(r => r.User)
                    .Include(r => r.Comments)
                    .Where(r => r.Comments.Count == 0 && r.Status != "Completed")
                    .OrderByDescending(r => r.SubmittedAt)
                    .Take(5)
                    .ToListAsync()
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
                        .Include(r => r.Comments)
                        .OrderByDescending(r => r.SubmittedAt)
                        .Take(10)
                        .ToListAsync(),
                    RecentUsers = await _context.Users
                        .OrderByDescending(u => u.CreatedAt)
                        .Take(5)
                        .ToListAsync(),
                    UnrespondedReportsCount = await _context.Reports
                        .Include(r => r.Comments)
                        .Where(r => r.Comments.Count == 0 && r.Status != "Completed")
                        .CountAsync(),
                    UnrespondedReports = await _context.Reports
                        .Include(r => r.User)
                        .Include(r => r.Comments)
                        .Where(r => r.Comments.Count == 0 && r.Status != "Completed")
                        .OrderByDescending(r => r.SubmittedAt)
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
                _logger.LogInformation($"Processing quick action on report {reportId}, action: {action}");

                var report = await _context.Reports
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null)
                {
                    _logger.LogError($"Report {reportId} not found");
                    return NotFound();
                }

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
                            $"Your report '{reportId}' has been accepted",
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
                            $"Your report '{reportId}' has been rejected",
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
                        _logger.LogError($"Invalid action: {action}");
                        return BadRequest("Invalid action specified");
                }

                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = $"Report #{reportId} has been {action.ToLower()}ed successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing quick action on report {reportId}");
               
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
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> DeleteReport(int reportId)
        {
            try
            {
                // Load the report with ALL related entities
                var report = await _context.Reports
                    .Include(r => r.Comments)
                    .Include(r => r.StatusHistory)
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ResolvedBy)
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null)
                {
                    TempData["ErrorMessage"] = "Report not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Validate report status
                if (report.Status != "Completed" && report.Status != "Rejected")
                {
                    TempData["ErrorMessage"] = "Only completed or rejected reports can be deleted.";
                    return RedirectToAction(nameof(Index));
                }

                // Use a transaction to ensure all operations succeed or fail together
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Delete associated notifications
                    var notifications = await _context.Notifications
                        .Where(n => n.ReportId == reportId)
                        .ToListAsync();
                    _context.Notifications.RemoveRange(notifications);

                    // Delete associated images
                    if (!string.IsNullOrEmpty(report.ImagePath))
                    {
                        var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, report.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }

                    // Delete comment images
                    foreach (var comment in report.Comments.Where(c => !string.IsNullOrEmpty(c.ImagePath)))
                    {
                        var commentImagePath = Path.Combine(_webHostEnvironment.WebRootPath, comment.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(commentImagePath))
                        {
                            System.IO.File.Delete(commentImagePath);
                        }
                    }

                    // Remove all related entities
                    _context.ReportComments.RemoveRange(report.Comments);
                    _context.ReportStatusHistories.RemoveRange(report.StatusHistory);

                    // Clear navigation properties to avoid reference issues
                    report.AssignedTo = null;
                    report.ResolvedBy = null;
                    report.User = null;

                    // Delete the report
                    _context.Reports.Remove(report);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["StatusMessage"] = $"Report #{reportId} has been deleted successfully.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error deleting report {reportId}");
                    TempData["ErrorMessage"] = $"Error deleting report: {ex.Message}";

                    // Log inner exception if it exists
                    if (ex.InnerException != null)
                    {
                        _logger.LogError(ex.InnerException, "Inner exception for report deletion");
                        TempData["ErrorMessage"] += $" Details: {ex.InnerException.Message}";
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in delete report workflow {reportId}");
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
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
                    .Select(r => new
                    {
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
        [ValidateAntiForgeryToken]
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

                // Update report status if this is the first comment and report is pending
                var statusChanged = false;
                if (report.Status == "Pending" && report.Comments.Count == 0)
                {
                    report.Status = "In Progress";
                    report.AssignedToId = staffId;
                    report.AssignedAt = DateTime.UtcNow;
                    statusChanged = true;
                    await RecordStatusChange(report, "Pending", "In Progress",
                        "Status changed to In Progress after first admin comment");
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    commentContent = comment,
                    imagePath = imagePath,
                    authorName = $"{staffUser.FirstName} {staffUser.LastName}",
                    authorEmail = staffUser.Email,
                    createdAt = DateTime.Now.ToString("f"),
                    statusChanged = statusChanged,
                    newStatus = statusChanged ? "In Progress" : report.Status,
                    newStatusClass = statusChanged ? "bg-info" : GetStatusBadgeClass(report.Status),
                    newStatusIcon = statusChanged ? "fa-spinner fa-pulse" : GetStatusIcon(report.Status)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding comment to report {reportId}");
                return Json(new { success = false, message = "Error adding comment" });
            }
        }
        private string GetStatusBadgeClass(string status)
        {
            return status switch
            {
                "Pending" => "bg-warning",
                "In Progress" => "bg-info",
                "Completed" => "bg-success",
                "Rejected" => "bg-danger",
                _ => "bg-secondary"
            };
        }

        private string GetStatusIcon(string status)
        {
            return status switch
            {
                "Pending" => "fa-clock",
                "In Progress" => "fa-spinner fa-pulse",
                "Completed" => "fa-check-circle",
                "Rejected" => "fa-times-circle",
                _ => "fa-question-circle"
            };
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageUsers()
        {
            try
            {
                var users = await _context.Users
                    .OrderBy(u => u.LastName)
                    .ToListAsync();

                var availableRoles = new List<string> { "Admin", "Staff", "Professor", "Student" };

                var model = new StaffDashboardViewModel
                {
                    Users = users.Select(u => new UserManagementViewModel
                    {
                        Id = u.Id,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        CurrentRole = u.Role,
                        CreatedAt = u.CreatedAt
                    }).ToList(),
                    AvailableRoles = availableRoles.Select(r => new SelectListItem
                    {
                        Value = r,
                        Text = r
                    }).ToList()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user management page");
                TempData["ErrorMessage"] = "Error loading user data";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole(int userId, string newRole)
        {
            try
            {
                // Prevent modifying the current admin user's role
                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                if (userId == currentUserId)
                {
                    TempData["ErrorMessage"] = "You cannot change your own role";
                    return RedirectToAction("ManageUsers");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                // Validate the role
                var validRoles = new[] { "Admin", "Staff", "Professor", "Student" };
                if (!validRoles.Contains(newRole))
                {
                    TempData["ErrorMessage"] = "Invalid role specified";
                    return RedirectToAction("ManageUsers");
                }

                user.Role = newRole;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Successfully updated role for {user.Email} to {newRole}";
                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating role for user {userId}");
                TempData["ErrorMessage"] = "Error updating user role";
                return RedirectToAction("ManageUsers");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                // Prevent deleting the current user
                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                if (userId == currentUserId)
                {
                    TempData["ErrorMessage"] = "You cannot delete your own account";
                    return RedirectToAction("ManageUsers");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                // Delete all reports associated with this user first
                var userReports = await _context.Reports
                    .Where(r => r.UserId == userId)
                    .ToListAsync();

                if (userReports.Any())
                {
                    _context.Reports.RemoveRange(userReports);
                    await _context.SaveChangesAsync();
                }

                // Now delete the user
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                TempData["StatusMessage"] = $"Successfully deleted user {user.Email} and {userReports.Count} associated reports";
                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user {userId}");
                TempData["ErrorMessage"] = "Error deleting user";
                return RedirectToAction("ManageUsers");
            }

        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadLogo(IFormFile logoFile)
        {
            try
            {
                // Validate file exists
                if (logoFile == null || logoFile.Length == 0)
                    return Json(new { success = false, message = "No file selected" });

                // Validate extension
                var validExtensions = new[] { ".png", ".jpg", ".jpeg", ".svg" };
                var fileExt = Path.GetExtension(logoFile.FileName).ToLower();
                if (!validExtensions.Contains(fileExt))
                    return Json(new
                    {
                        success = false,
                        message = $"Invalid file type. Allowed: {string.Join(", ", validExtensions)}"
                    });

                // Validate size (5MB max)
                if (logoFile.Length > 5 * 1024 * 1024)
                    return Json(new
                    {
                        success = false,
                        message = "File too large (max 5MB)"
                    });

                // Ensure directory exists
                var imagesPath = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                if (!Directory.Exists(imagesPath))
                {
                    Directory.CreateDirectory(imagesPath);
                }

                // Delete old logos (all extensions)
                var oldLogos = Directory.GetFiles(imagesPath, "logo.*");
                foreach (var oldLogo in oldLogos)
                {
                    System.IO.File.Delete(oldLogo);
                }

                // Save new logo with standardized name (always use .jpg extension for JPEG files)
                var standardizedExt = fileExt == ".jpeg" ? ".jpg" : fileExt;
                var newFileName = "logo" + standardizedExt;
                var savePath = Path.Combine(imagesPath, newFileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(stream);
                }

                // Return the new logo URL with cache-buster
                var logoUrl = $"/images/{newFileName}?v={DateTime.Now.Ticks}";

                return Json(new
                {
                    success = true,
                    message = "Logo uploaded successfully",
                    logoUrl = logoUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading logo");
                return Json(new
                {
                    success = false,
                    message = $"Upload failed: {ex.Message}"
                });
            }
        }
        [HttpGet]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> DownloadReport(int id, string format)
        {
            try
            {
                var report = await _context.Reports
                    .Include(r => r.User)
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ResolvedBy)
                    .Include(r => r.Comments)
                        .ThenInclude(c => c.Author)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (report == null)
                {
                    return NotFound();
                }

                byte[] fileContents;
                string contentType;
                string fileName;

                if (format.ToLower() == "pdf")
                {
                    // Generate PDF
                    var converter = new SynchronizedConverter(new PdfTools());
                    var doc = new HtmlToPdfDocument()
                    {
                        GlobalSettings = {
                    ColorMode = ColorMode.Color,
                    Orientation = DinkToPdf.Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                    DocumentTitle = $"Report_{report.Id}"
                },
                        Objects = {
                    new ObjectSettings() {
                        HtmlContent = GenerateReportHtml(report, true),
                        WebSettings = { DefaultEncoding = "utf-8" },
                        HeaderSettings = { FontSize = 9, Right = "Page [page] of [toPage]", Line = true },
                        FooterSettings = { FontSize = 9, Center = $"Report generated on {DateTime.Now.ToString("f")}" }
                    }
                }
                    };

                    fileContents = converter.Convert(doc);
                    contentType = "application/pdf";
                    fileName = $"Report_{report.Id}.pdf";
                }
                else if (format.ToLower() == "docx")
                {
                    // Generate DOCX
                    using (var stream = new MemoryStream())
                    {
                        using (var doc = DocX.Create(stream))
                        {
                            // Create formatting
                            var headingFormat = new Formatting
                            {
                                Bold = true,
                                Size = 18,

                            };

                            var subHeadingFormat = new Formatting
                            {
                                Bold = true,
                                Size = 14,

                            };

                            // Add title
                            var title = doc.InsertParagraph($"Report #{report.Id}: {report.Title}", false, headingFormat);
                            title.SpacingAfter(20);

                            // Add metadata
                            var metadata = doc.InsertParagraph();
                            metadata.AppendLine($"Submitted by: {report.User?.FirstName} {report.User?.LastName}");
                            metadata.AppendLine($"Date: {report.SubmittedAt.ToString("f")}");
                            metadata.AppendLine($"Status: {report.Status}");
                            metadata.SpacingAfter(20);

                            // Add description
                            var descTitle = doc.InsertParagraph("Description:", false, subHeadingFormat);
                            descTitle.SpacingAfter(5);

                            var description = doc.InsertParagraph(report.Description);
                            description.SpacingAfter(20);

                            // Add location
                            var location = doc.InsertParagraph($"Location: {report.Location}");
                            location.SpacingAfter(20);

                            // Add image if exists
                            if (!string.IsNullOrEmpty(report.ImagePath))
                            {
                                try
                                {
                                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, report.ImagePath.TrimStart('/'));
                                    if (System.IO.File.Exists(imagePath))
                                    {
                                        var imageTitle = doc.InsertParagraph("Attached Image:", false, subHeadingFormat);
                                        imageTitle.SpacingAfter(5);

                                        using (var imageStream = new FileStream(imagePath, FileMode.Open))
                                        {
                                            var image = doc.AddImage(imageStream);
                                            var picture = image.CreatePicture(400, 300); // Adjust size as needed
                                            doc.InsertParagraph().InsertPicture(picture);
                                        }
                                        doc.InsertParagraph().SpacingAfter(20);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error adding image to DOCX report");
                                }
                            }

                            if (report.ResolvedBy != null)
                            {
                                var resolutionTitle = doc.InsertParagraph("Resolution Details:", false, subHeadingFormat);
                                resolutionTitle.SpacingAfter(5);

                                var resolution = doc.InsertParagraph();
                                resolution.AppendLine($"Resolved by: {report.ResolvedBy.FirstName} {report.ResolvedBy.LastName}");
                                resolution.AppendLine($"Resolved at: {report.ResolvedAt?.ToString("f")}");
                                resolution.AppendLine();
                                resolution.Append("Resolution Notes: ").Bold();
                                resolution.Append(report.ResolutionNotes);
                                resolution.SpacingAfter(20);
                            }

                            // Add comments if any
                            if (report.Comments.Any())
                            {
                                var commentsTitle = doc.InsertParagraph("Comments:", false, subHeadingFormat);
                                commentsTitle.SpacingAfter(5);

                                foreach (var comment in report.Comments)
                                {
                                    var commentPara = doc.InsertParagraph();
                                    commentPara.Append($"{comment.Author?.FirstName} {comment.Author?.LastName} - {comment.CreatedAt.ToString("f")}: ").Bold();
                                    commentPara.Append(comment.Content);

                                    // Add comment image if exists
                                    if (!string.IsNullOrEmpty(comment.ImagePath))
                                    {
                                        try
                                        {
                                            var commentImagePath = Path.Combine(_webHostEnvironment.WebRootPath, comment.ImagePath.TrimStart('/'));
                                            if (System.IO.File.Exists(commentImagePath))
                                            {
                                                using (var imageStream = new FileStream(commentImagePath, FileMode.Open))
                                                {
                                                    var image = doc.AddImage(imageStream);
                                                    var picture = image.CreatePicture(300, 200); // Smaller size for comment images
                                                    doc.InsertParagraph().InsertPicture(picture);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "Error adding comment image to DOCX report");
                                        }
                                    }

                                    commentPara.SpacingAfter(10);
                                }
                            }

                            doc.Save();
                        }
                         stream.Position = 0;
                        fileContents = stream.ToArray();
                    }
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    fileName = $"Report_{report.Id}.docx";
                }
                else
                {
                    return BadRequest("Invalid format specified");
                }

                return File(fileContents, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating report download for report {id}");
                TempData["ErrorMessage"] = "Error generating report download";
                return RedirectToAction("ViewReport", new { id = id });
            }
        }

        private string GenerateReportHtml(Reports report, bool includeImages)
        {
            var imageHtml = "";

            if (includeImages && !string.IsNullOrEmpty(report.ImagePath))
            {
                try
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, report.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        var imageBytes = System.IO.File.ReadAllBytes(imagePath);
                        var base64Image = Convert.ToBase64String(imageBytes);
                        var imageType = Path.GetExtension(report.ImagePath).TrimStart('.').ToLower();
                        imageType = imageType == "jpg" ? "jpeg" : imageType;

                        imageHtml = $@"
                <div class='section'>
                    <p class='label'>Attached Image:</p>
                    <img src='data:image/{imageType};base64,{base64Image}' style='max-width: 100%; max-height: 400px;' />
                </div>";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding image to HTML report");
                }
            }

            var commentsHtml = "";
            if (report.Comments.Any())
            {
                commentsHtml = "<div class='section'><h2>Comments</h2>";

                foreach (var comment in report.Comments)
                {
                    var commentImageHtml = "";
                    if (includeImages && !string.IsNullOrEmpty(comment.ImagePath))
                    {
                        try
                        {
                            var commentImagePath = Path.Combine(_webHostEnvironment.WebRootPath, comment.ImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(commentImagePath))
                            {
                                var imageBytes = System.IO.File.ReadAllBytes(commentImagePath);
                                var base64Image = Convert.ToBase64String(imageBytes);
                                var imageType = Path.GetExtension(comment.ImagePath).TrimStart('.').ToLower();
                                imageType = imageType == "jpg" ? "jpeg" : imageType;

                                commentImageHtml = $@"
                        <div style='margin-top: 10px;'>
                            <img src='data:image/{imageType};base64,{base64Image}' style='max-width: 100%; max-height: 300px;' />
                        </div>";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error adding comment image to HTML report");
                        }
                    }

                    commentsHtml += $@"
            <div style='margin-bottom: 15px; border-left: 3px solid #3498db; padding-left: 10px;'>
                <p><strong>{comment.Author?.FirstName} {comment.Author?.LastName}</strong> - {comment.CreatedAt.ToString("f")}</p>
                <p>{comment.Content}</p>
                {commentImageHtml}
            </div>";
                }

                commentsHtml += "</div>";
            }

            return $@"
<html>
    <head>
        <style>
            body {{ font-family: Arial, sans-serif; line-height: 1.6; margin: 20px; }}
            h1 {{ color: #2c3e50; border-bottom: 1px solid #3498db; padding-bottom: 5px; }}
            h2 {{ color: #3498db; font-size: 1.3em; margin-top: 25px; }}
            .section {{ margin-bottom: 20px; }}
            .label {{ font-weight: bold; color: #3498db; }}
            img {{ border: 1px solid #ddd; border-radius: 4px; }}
        </style>
    </head>
    <body>
        <h1>Report #{report.Id}: {report.Title}</h1>
        
        <div class='section'>
            <p><span class='label'>Submitted by:</span> {report.User?.FirstName} {report.User?.LastName}</p>
            <p><span class='label'>Date:</span> {report.SubmittedAt.ToString("f")}</p>
            <p><span class='label'>Status:</span> {report.Status}</p>
        </div>
        
        <div class='section'>
            <p class='label'>Description:</p>
            <p>{report.Description}</p>
        </div>
        
        <div class='section'>
            <p><span class='label'>Location:</span> {report.Location}</p>
        </div>
        
        {imageHtml}
        
        {(report.ResolvedBy != null ? $@"
        <div class='section'>
            <h2>Resolution Details</h2>
            <p><span class='label'>Resolved by:</span> {report.ResolvedBy.FirstName} {report.ResolvedBy.LastName}</p>
            <p><span class='label'>Resolved at:</span> {report.ResolvedAt?.ToString("f")}</p>
            <p class='label'>Resolution Notes:</p>
            <p>{report.ResolutionNotes}</p>
        </div>" : "")}
        
        {commentsHtml}
    </body>
</html>";
        }
    }
}