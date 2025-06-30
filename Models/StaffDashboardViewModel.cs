using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using UnivMate.Models;

namespace UnivMate.ViewModels
{
    public class StaffDashboardViewModel
    {
        // Statistics
        public int TotalUsers { get; set; }
        public int TotalStudents { get; set; }
        public int TotalProfessors { get; set; }
        public int TotalStaff { get; set; }
        public int ActiveToday { get; set; }
        public int PendingReports { get; set; }
        public int ResolvedReports { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string CurrentStatus { get; set; }
        public int InProgressReports { get; set; }
        public int CompletedReports { get; set; }
        public int RejectedReports { get; set; }
      

        public int UnrespondedReportsCount { get; set; }
        public List<Reports> UnrespondedReports { get; set; }

        // Collections
        public List<Reports> Reports { get; set; } = new List<Reports>(); // Changed from RecentReports to Reports
        public List<User> RecentUsers { get; set; } = new List<User>();

        public List<UserManagementViewModel> Users { get; set; }
        public List<SelectListItem> AvailableRoles { get; set; }
        public Dictionary<int, int> DaysSinceLastResponse { get; set; } = new Dictionary<int, int>();
    }

    public class UserManagementViewModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string CurrentRole { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
