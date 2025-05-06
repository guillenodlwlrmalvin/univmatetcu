using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UnivMate.Data;
using UnivMate.Models;
using UnivMate.ViewModels;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Hosting;

namespace UnivMate.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IWebHostEnvironment _env;

        public AccountController(
            ApplicationDbContext context,
            ILogger<AccountController> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }

        // ==================== LOGIN ====================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDashboard();
            }

            await VerifyAdminAccount();
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());

            if (user == null || !VerifyPassword(model.Password, user.PasswordHash))
            {
                _logger.LogWarning("Failed login attempt for email: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            // Update last login
            user.LastLoginDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await SignInUserAsync(user);
            _logger.LogInformation("User {UserId} logged in successfully", user.Id);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToDashboard(user);
        }

        private IActionResult RedirectToDashboard(User? user = null)
        {
            var role = user?.Role ?? User.FindFirstValue(ClaimTypes.Role);
            return role switch
            {
                "Staff" or "Admin" => RedirectToAction("Index", "StaffDashboard"),
                _ => RedirectToAction("Index", "Dashboard")
            };
        }

        // ==================== ADMIN ACCOUNT VERIFICATION ====================
        private async Task VerifyAdminAccount()
        {
            const string adminEmail = "admin@univmate.com";

            var adminExists = await _context.Users
                .AnyAsync(u => u.Email == adminEmail && u.Role == "Admin");

            if (!adminExists)
            {
                _logger.LogWarning("Admin account not found in database. Please ensure migrations are applied.");
            }
        }

        [HttpGet]
        public IActionResult AccessDenied(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "StaffDashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // ==================== AUTHENTICATION HELPERS ====================
        private async Task SignInUserAsync(User user)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
                    AllowRefresh = true
                });
        }

        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                var hashOfInput = HashPassword(password);
                return string.Equals(hashOfInput, storedHash, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        // ==================== REGISTER ====================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            ModelState.Remove("Department");
            ModelState.Remove("Position");
            ModelState.Remove("StaffId");
            ModelState.Remove("OfficeLocation");
            ModelState.Remove("StudentId");
            ModelState.Remove("Major");

            if (!ModelState.IsValid)
                return View(model);

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Email is already registered.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == model.Username.ToLower()))
            {
                ModelState.AddModelError("Username", "Username is already taken.");
                return View(model);
            }

            var user = new User
            {
                Username = model.Username.Trim(),
                Email = model.Email.Trim().ToLower(),
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                PasswordHash = HashPassword(model.Password),
                Role = model.Role,
                CreatedAt = DateTime.UtcNow
            };

            switch (model.Role)
            {
                case "Student":
                    user.StudentId = model.StudentId?.Trim();
                    user.Major = model.Major?.Trim();
                    break;
                case "Professor":
                    user.Department = model.Department?.Trim();
                    user.Position = model.Position?.Trim();
                    break;
                case "Staff":
                    user.StaffId = model.StaffId?.Trim();
                    user.OfficeLocation = model.OfficeLocation?.Trim();
                    break;
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["RegistrationSuccess"] = "Registration successful. Please log in.";
            return RedirectToAction("Login");
        }

        // ==================== STAFF DASHBOARD ====================
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> StaffDashboard()
        {
            var reports = await _context.Reports
                .Include(r => r.User)
                .OrderByDescending(r => r.SubmittedAt)
                .Take(10)
                .ToListAsync();

            var model = new StaffDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalStudents = await _context.Users.CountAsync(u => u.Role == "Student"),
                TotalProfessors = await _context.Users.CountAsync(u => u.Role == "Professor"),
                TotalStaff = await _context.Users.CountAsync(u => u.Role == "Staff"),
                ActiveToday = await _context.Users.CountAsync(u => u.LastLoginDate >= DateTime.UtcNow.AddDays(-1)),
                PendingReports = await _context.Reports.CountAsync(r => r.Status == "Pending"),
                ResolvedReports = await _context.Reports.CountAsync(r => r.Status == "Resolved"),
                Reports = reports
            };

            return View(model);
        }

        // ==================== LOGOUT ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // ==================== EDIT PROFILE ====================
        [Authorize]
        public async Task<IActionResult> EditProfile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                await HttpContext.SignOutAsync();
                return RedirectToAction("Login");
            }

            return View(new EditProfileViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ExistingProfilePicturePath = user.ProfilePicturePath,
                Role = user.Role,
                StudentId = user.StudentId,
                Major = user.Major,
                Department = user.Department,
                Position = user.Position,
                StaffId = user.StaffId,
                OfficeLocation = user.OfficeLocation
            });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                await HttpContext.SignOutAsync();
                return RedirectToAction("Login");
            }

            ModelState.Remove("Department");
            ModelState.Remove("Position");
            ModelState.Remove("StaffId");
            ModelState.Remove("OfficeLocation");
            ModelState.Remove("StudentId");
            ModelState.Remove("Major");

            if (!ModelState.IsValid)
            {
                model.ExistingProfilePicturePath = user.ProfilePicturePath;
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower() && u.Id != userId))
            {
                ModelState.AddModelError("Email", "This email is already in use.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == model.Username.ToLower() && u.Id != userId))
            {
                ModelState.AddModelError("Username", "This username is already taken.");
                return View(model);
            }

            // Update basic info
            user.Username = model.Username.Trim();
            user.Email = model.Email.Trim().ToLower();
            user.FirstName = model.FirstName.Trim();
            user.LastName = model.LastName.Trim();

            // Handle profile picture
            if (model.ProfilePicture != null && model.ProfilePicture.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(model.ProfilePicture.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ProfilePicture", "Only JPG, JPEG or PNG files are allowed.");
                    return View(model);
                }

                if (model.ProfilePicture.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ProfilePicture", "The file is too large (max 5MB).");
                    return View(model);
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profile-pictures");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePicture.CopyToAsync(fileStream);
                }

                if (!string.IsNullOrEmpty(user.ProfilePicturePath))
                {
                    var oldFilePath = Path.Combine(_env.WebRootPath, user.ProfilePicturePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                user.ProfilePicturePath = $"/uploads/profile-pictures/{uniqueFileName}";
            }

            // Update role-specific properties
            switch (user.Role)
            {
                case "Student":
                    user.StudentId = model.StudentId?.Trim();
                    user.Major = model.Major?.Trim();
                    break;
                case "Professor":
                    user.Department = model.Department?.Trim();
                    user.Position = model.Position?.Trim();
                    break;
                case "Staff":
                case "Admin":
                    user.StaffId = model.StaffId?.Trim();
                    user.OfficeLocation = model.OfficeLocation?.Trim();
                    break;
            }

            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                await HttpContext.SignOutAsync();
                await SignInUserAsync(user);

                TempData["SuccessMessage"] = "Profile updated successfully!";
                return RedirectToAction("EditProfile");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                ModelState.AddModelError("", "An error occurred while saving your profile. Please try again.");
                return View(model);
            }
        }

        // ==================== DEBUG ENDPOINTS ====================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult DebugHash(string password = "Admin@123")
        {
            return Content($"Hash for '{password}': {HashPassword(password)}");
        }

        [HttpGet]
        [Authorize]
        public IActionResult ViewClaims()
        {
            var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}");
            return Content(string.Join("\n", claims));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> LoginAsAdmin()
        {
            var admin = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == "admin@univmate.com" && u.Role == "Admin");

            if (admin == null)
            {
                return Content("Admin account not found. Please apply database migrations.");
            }

            await SignInUserAsync(admin);
            return RedirectToAction("Index", "StaffDashboard");
        }
        [HttpGet("get-correct-hash")]
        public IActionResult GetCorrectHash()
        {
            return Content(HashPassword("Admin@123"));
        }

    }
}