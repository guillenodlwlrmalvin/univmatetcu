using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using UnivMate.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Collections.Generic;
using UnivMate.Data;
using UnivMate.ViewModels;

namespace UnivMate.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Dashboard
        public IActionResult Index()
        {
            var userName = User.Identity?.Name;
            ViewBag.UserName = userName;
            return View();
        }

        // POST: /Dashboard/SubmitReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReport(string title, string description, string location, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var userName = User.Identity?.Name;

                    // Handle file upload
                    string imagePath = null;
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        imagePath = "/uploads/" + uniqueFileName;
                    }

                    // Create new report with initial status history
                    var report = new Reports
                    {
                        Title = title,
                        Description = description,
                        Location = location,
                        ImagePath = imagePath,
                        Status = "Pending",
                        SubmittedAt = DateTime.Now,
                        UserId = int.Parse(userId), // Make sure this matches your User model's ID type
                        StatusHistory = new List<ReportStatusHistory>
                        {
                            new ReportStatusHistory
                            {
                                OldStatus = null,
                                NewStatus = "Pending",
                                ChangedBy = userName,
                                ChangedAt = DateTime.Now,
                                Notes = "Report submitted"
                            }
                        }
                    };

                    _context.Reports.Add(report);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Report submitted successfully!" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Error submitting report: " + ex.Message });
                }
            }

            return Json(new { success = false, message = "Invalid form data" });
        }

        // GET: /Dashboard/GetAllReports
        [HttpGet]
        public async Task<IActionResult> GetMyReports()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var reports = await _context.Reports
                    .Where(r => r.UserId == int.Parse(userId))
                    .Include(r => r.User)
                    .OrderByDescending(r => r.SubmittedAt)
                    .Select(r => new
                    {
                        id = r.Id,
                        title = r.Title ?? (r.Description.Length > 50 ? r.Description.Substring(0, 50) + "..." : r.Description),
                        description = r.Description,
                        userName = r.User.Username,
                        submittedAt = r.SubmittedAt,
                        location = r.Location,
                        status = r.Status,
                        imagePath = r.ImagePath
                    })
                    .ToListAsync();

                return Json(new { success = true, reports });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading reports: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReportDetails(int id)
        {
            try
            {
                var report = await _context.Reports
                    .Include(r => r.User)
                    .Include(r => r.StatusHistory)
                    .Where(r => r.Id == id)
                    .Select(r => new
                    {
                        id = r.Id,
                        title = r.Title,
                        description = r.Description,
                        userName = r.User.Username,
                        submittedAt = r.SubmittedAt,
                        location = r.Location,
                        status = r.Status,
                        imagePath = Url.Content("~" + r.ImagePath), // This ensures proper URL generation
                    


                        statusHistory = r.StatusHistory
                            .OrderByDescending(sh => sh.ChangedAt)
                            .Select(sh => new
                            {
                                oldStatus = sh.OldStatus,
                                newStatus = sh.NewStatus,
                                changedBy = sh.ChangedBy,
                                changedAt = sh.ChangedAt,
                                notes = sh.Notes
                            })
                    })
                    .FirstOrDefaultAsync();

                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                return Json(new { success = true, report });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading report details: " + ex.Message });
            }
        }
        // GET: /Dashboard/ViewReport/{id}
        public async Task<IActionResult> ViewReport(int id)
        {
            var report = await _context.Reports
                .Include(r => r.User)
                .Include(r => r.StatusHistory)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            return View(report);
        }

        // POST: /Dashboard/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int reportId, string newStatus, string notes)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User.Identity?.Name;

                var report = await _context.Reports
                    .Include(r => r.StatusHistory)
                    .FirstOrDefaultAsync(r => r.Id == reportId);

                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                // Add status history
                report.StatusHistory.Add(new ReportStatusHistory
                {
                    OldStatus = report.Status,
                    NewStatus = newStatus,
                    ChangedBy = userName,
                    ChangedAt = DateTime.Now,
                    Notes = notes
                });

                // Update report status
                report.Status = newStatus;

                // Update additional fields based on status
                if (newStatus == "In Progress" && report.AssignedToId == null)
                {
                    report.AssignedToId = int.Parse(userId);
                    report.AssignedAt = DateTime.Now;
                }
                else if (newStatus == "Completed")
                {
                    report.ResolvedById = int.Parse(userId);
                    report.ResolvedAt = DateTime.Now;
                    report.ResolutionNotes = notes;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Status updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating status: " + ex.Message });
            }
        }

        // GET: /Dashboard/EditProfile
        public async Task<IActionResult> EditProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(int.Parse(userId));

            if (user == null)
            {
                return NotFound();
            }

            var model = new EditProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                // Add other properties as needed
            };

            return View(model);
        }

        // POST: /Dashboard/EditProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users.FindAsync(int.Parse(userId));

                if (user == null)
                {
                    return NotFound();
                }

                // Update user properties
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                // Update other properties as needed

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your profile has been updated successfully.";
                return RedirectToAction("Index");
            }

            TempData["ErrorMessage"] = "There was an error updating your profile.";
            return View(model);
        }
        public async Task<IActionResult> StaffDashboard()
        {
            var model = new StaffDashboardViewModel
            {
                // Count all users
                TotalUsers = await _context.Users.CountAsync(),

                // Count students (assuming you have a Role property)
                TotalStudents = await _context.Users
                    .Where(u => u.Role == "Student")
                    .CountAsync(),

                // Count professors
                TotalProfessors = await _context.Users
                    .Where(u => u.Role == "Professor")
                    .CountAsync(),

                // Count pending reports
                PendingReports = await _context.Reports
                    .Where(r => r.Status == "Pending")
                    .CountAsync(),

                // Other properties you need
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
    }

}
