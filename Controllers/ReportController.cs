using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnivMate.Data;
using UnivMate.Models;
using UnivMate.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using UnivMate.Hubs;
using System.ComponentModel.DataAnnotations;

namespace UnivMate.Controllers
{
    [Authorize]
    public class ReportsController : Controller  // Changed from ReportController to ReportsController for consistency
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<ReportHub> _hubContext;

        public ReportsController(
            ApplicationDbContext context,
            IWebHostEnvironment env, IHubContext<ReportHub> hubContext)
        {
            _context = context;
            _env = env;
            _hubContext = hubContext;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReport([FromForm] ReportViewModel model)
        {
           
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Json(new
                {
                    success = false,               
                    errors
                });
            }

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "User not found"
                    });
                }

                // File validation
                if (model.ImageFile == null || model.ImageFile.Length == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Please upload an image file"
                    });
                }

                if (model.ImageFile.Length > 5 * 1024 * 1024)
                {
                    return Json(new
                    {
                        success = false,
                        message = "File size exceeds 5MB limit"
                    });
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(model.ImageFile.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Only JPG, JPEG or PNG files are allowed"
                    });
                }

                // Save file
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                var imagePath = $"/uploads/{uniqueFileName}";

                // Create report
                var report = new Reports  // Changed from Reports to Report for consistency
                {
                    Title = $"{model.Location} Issue - {DateTime.Now:MMM dd}",
                    Description = model.Description,
                    Location = model.Location,
                    LocationGroup = model.LocationGroup,  // Add this
                    LocationSubgroup = model.LocationSubgroup,  // Add this
                    ImagePath = imagePath,
                    Status = "Pending",
                    SubmittedAt = DateTime.Now,
                    UserId = user.Id
                };

                _context.Reports.Add(report);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group("Staff").SendAsync("ReceiveReportNotification", new
                {
                    type = "NewReport",
                    id = report.Id,
                    title = report.Title,
                    location = report.Location,
                    submittedAt = report.SubmittedAt,
                    userId = report.UserId,
                    userName = user.FirstName // Assuming you have FirstName in your User model
                });
                return Json(new
                {
                    success = true,
                    message = "Report submitted successfully!",
                    reportId = report.Id
                });
            }

            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,               
                    error = ex.Message
                });

            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserReports()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var reports = await _context.Reports
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.SubmittedAt)
                    .Select(r => new
                    {
                        id = r.Id,
                        title = r.Title,
                        description = r.Description,
                        status = r.Status,
                        submittedAt = r.SubmittedAt.ToString("MM/dd/yyyy hh:mm tt"),
                        location = r.Location,
                        imagePath = r.ImagePath
                    })
                    .ToListAsync();

                return Json(new { success = true, reports });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading reports",
                    error = ex.Message
                });
            }
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAllReports()
        {
            try
            {
                var reports = await _context.Reports
                    .Include(r => r.User)
                    .OrderByDescending(r => r.SubmittedAt)
                    .Select(r => new
                    {
                        id = r.Id,
                        title = r.Title,
                        userName = r.User != null ? r.User.FirstName : "Unknown",
                        submittedAt = r.SubmittedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                        location = r.Location,
                        status = r.Status,
                        imagePath = r.ImagePath
                    })
                    .AsNoTracking() // Improves performance for read-only operations
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    reports,
                    count = reports.Count
                });
            }
            catch (Exception ex)
            {
                // Log the error here (implement your logging solution)
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load reports",
                    error = ex.Message
                });
            }
        }

        public async Task<IActionResult> ViewReport(int id)
        {
            try
            {
                var report = await _context.Reports
                    .Include(r => r.User)
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ResolvedBy)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (report == null)
                {
                    return NotFound();
                }

                // Authorization check
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var isStaffOrAdmin = User.IsInRole("Staff") || User.IsInRole("Admin");

                if (report.UserId != userId && !isStaffOrAdmin)
                {
                    return Forbid();
                }

                return View(report);
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReportStatus(int reportId, string status, string resolutionNotes)
        {
            try
            {
                var report = await _context.Reports.FindAsync(reportId);
                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Users.FindAsync(userId);

                report.Status = status;
                report.ResolutionNotes = resolutionNotes;

                if (status == "In Progress")
                {
                    report.AssignedToId = userId;
                    report.AssignedAt = DateTime.Now;
                }
                else if (status == "Completed" || status == "Rejected")
                {
                    report.ResolvedById = userId;
                    report.ResolvedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Report status updated successfully",
                    updatedStatus = status,
                    updatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error updating report",
                    error = ex.Message
                });
            }
        }

        // Other actions remain similar but with consistent JSON responses
        // ...

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessReport(int reportId, string action)
        {
            try
            {
                var report = await _context.Reports.FindAsync(reportId);
                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Users.FindAsync(userId);

                string newStatus = action == "accept" ? "In Progress" : "Rejected";
                report.Status = newStatus;

                if (action == "accept")
                {
                    report.AssignedToId = userId;
                    report.AssignedAt = DateTime.Now;
                }
                else
                {
                    report.ResolvedById = userId;
                    report.ResolvedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Report {action}ed successfully",
                    newStatus,
                    updatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error {action}ing report",
                    error = ex.Message
                });
            }
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteReport(int reportId, string resolutionNotes)
        {
            try
            {
                var report = await _context.Reports.FindAsync(reportId);
                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Users.FindAsync(userId);

                report.Status = "Completed";
                report.ResolutionNotes = resolutionNotes;
                report.ResolvedById = userId;
                report.ResolvedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Report marked as completed",
                    updatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error completing report",
                    error = ex.Message
                });
            }
        }
    }
}