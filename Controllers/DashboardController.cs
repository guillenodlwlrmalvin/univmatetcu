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
                    .Include(r => r.AssignedTo)
                    .Include(r => r.ResolvedBy)
                    .Include(r => r.StatusHistory)
                    .Include(r => r.Comments)
                        .ThenInclude(c => c.Author)
                    .Where(r => r.Id == id)
                    .Select(r => new
                    {
                        id = r.Id,
                        title = r.Title,
                        description = r.Description,
                        location = r.Location,
                        status = r.Status,
                        imagePath = r.ImagePath,
                        submittedAt = r.SubmittedAt,
                        assignedAt = r.AssignedAt,
                        resolvedAt = r.ResolvedAt,
                        resolutionNotes = r.ResolutionNotes,
                        userName = r.User.FirstName + " " + r.User.LastName,
                        userEmail = r.User.Email,
                        rating = r.Rating,
                        canRate = r.Status == "Completed" && r.Rating == null && r.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)),
                        assignedTo = r.AssignedTo != null ? new
                        {
                            firstName = r.AssignedTo.FirstName,
                            lastName = r.AssignedTo.LastName,
                            email = r.AssignedTo.Email
                        } : null,
                        resolvedBy = r.ResolvedBy != null ? new
                        {
                            firstName = r.ResolvedBy.FirstName,
                            lastName = r.ResolvedBy.LastName,
                            email = r.ResolvedBy.Email
                        } : null,
                        statusHistory = r.StatusHistory
                            .OrderByDescending(sh => sh.ChangedAt)
                            .Select(sh => new
                            {
                                oldStatus = sh.OldStatus,
                                newStatus = sh.NewStatus,
                                changedBy = sh.ChangedBy,
                                changedAt = sh.ChangedAt,
                                notes = sh.Notes
                            }),
                        comments = r.Comments
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => new
                            {
                                id = c.Id,
                                content = c.Content,
                                imagePath = c.ImagePath,
                                createdAt = c.CreatedAt,
                                author = new
                                {
                                    id = c.Author.Id,
                                    firstName = c.Author.FirstName,
                                    lastName = c.Author.LastName,
                                    email = c.Author.Email,
                                    role = c.Author.Role
                                }
                            })
                    })
                    .FirstOrDefaultAsync();

                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                // Get the latest admin comment (if any)
                var adminComment = report.comments
                    .FirstOrDefault(c => c.author.role == "Admin" || c.author.role == "Staff");

                return Json(new
                {
                    success = true,
                    report = new
                    {
                        report.id,
                        report.title,
                        report.description,
                        report.location,
                        report.status,
                        report.imagePath,
                        report.submittedAt,
                        report.assignedAt,
                        report.resolvedAt,
                        report.resolutionNotes,
                        report.userName,
                        report.userEmail,
                        report.rating,
                        report.canRate,
                        report.assignedTo,
                        report.resolvedBy,
                        report.statusHistory,
                        adminComment = adminComment?.content,
                        adminCommentImage = adminComment?.imagePath,
                        adminCommentDate = adminComment?.createdAt,
                        comments = report.comments
                    }
                });
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRating([FromBody] RatingRequest request)
        {
            try
            {
                // Validate the request model
                if (request == null)
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                // Validate the rating value
                if (request.Rating < 1 || request.Rating > 5)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid rating value. Please provide a rating between 1 and 5 stars."
                    });
                }

                // Get current user
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "User not authenticated. Please log in again."
                    });
                }

                // Find the report with validation
                var report = await _context.Reports
                    .Include(r => r.User)
                    .Include(r => r.StatusHistory)
                    .FirstOrDefaultAsync(r => r.Id == request.ReportId);

                if (report == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Report not found. It may have been deleted."
                    });
                }

                // Verify report ownership
                if (report.UserId != int.Parse(userId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "You can only rate reports that you submitted."
                    });
                }

                // Check report status
                if (report.Status != "Completed")
                {
                    return Json(new
                    {
                        success = false,
                        message = "Only completed reports can be rated. Current status: " + report.Status
                    });
                }

                // Check if already rated
                if (report.Rating.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        message = "You've already rated this report."
                    });
                }

                // Update the report with rating
                report.Rating = request.Rating;
            

                // Add status history entry
                report.StatusHistory.Add(new ReportStatusHistory
                {
                    OldStatus = report.Status,
                    NewStatus = report.Status, // Status remains "Completed"
                    ChangedBy = User.Identity?.Name,
                    ChangedAt = DateTime.UtcNow,
                    Notes = $"User rated the resolution {request.Rating} star(s)"
                });

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Thank you for your rating!",
                    rating = request.Rating,
                    canRate = false
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "An error occurred while submitting your rating. Please try again."
                });
            }
        }


        [Authorize(Roles = "Admin,Staff")]
        public IActionResult UserDashboard()
        {
            // Get current user
            var userName = User.Identity?.Name;

            // You might want to pass some additional data if needed
            return View("~/Views/Dashboard/Index.cshtml", new
            {
                // Any data you want to pass to the user dashboard
            });
        }

        
        [HttpGet]
        public async Task<IActionResult> GetLocations()
        {
            // Replace this with your actual data access logic
            var locations = await _context.Locations
                .OrderBy(l => l.Group)
                .ThenBy(l => l.Subgroup)
                .ThenBy(l => l.Name)
                .ToListAsync();

            return Json(new
            {
                success = true,
                locations = locations.Select(l => new {
                    id = l.Id,
                    name = l.Name,
                    group = l.Group,
                    subgroup = l.Subgroup
                })
            });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        public async Task<IActionResult> AddLocation([FromBody] LocationModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid data" });
            }

            try
            {
                var location = new Location
                {
                    Name = model.Name,
                    Group = model.Group,
                    Subgroup = string.IsNullOrEmpty(model.Subgroup) ? null : model.Subgroup
                };

                _context.Locations.Add(location);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

       [HttpPost]
[Authorize(Roles = "Admin,Staff")]
public async Task<IActionResult> DeleteLocation([FromBody] DeleteLocationModel model)
{
    if (model == null || model.Id <= 0)
    {
        return Json(new { success = false, message = "Invalid location ID" });
    }

    try
    {
        var location = await _context.Locations.FindAsync(model.Id);
        if (location == null)
        {
            return Json(new { success = false, message = "Location not found" });
        }

        // Find all reports that reference this location
        var reportsWithLocation = await _context.Reports
            .Where(r => r.Location == location.Name)
            .ToListAsync();

        // Update those reports to use a placeholder location
        foreach (var report in reportsWithLocation)
        {
            report.Location = "[Deleted Location]";
        }

        // Remove the location
        _context.Locations.Remove(location);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Location deleted successfully" });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = $"Error deleting location: {ex.Message}" });
    }
}



        public class LocationModel
        {
            public string Name { get; set; }
            public string Group { get; set; }
            public string Subgroup { get; set; }
        }

        public class DeleteLocationModel
        {
            public int Id { get; set; }
        }

        public class RatingRequest
        {
            public int ReportId { get; set; }
            public int Rating { get; set; }
        }
    }
}
