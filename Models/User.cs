using System;
using System.Collections.Generic;

namespace UnivMate.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? ProfilePicturePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginDate { get; set; } // Added for tracking
        public string Role { get; set; }

        // Student fields
        public string? StudentId { get; set; }
        public string? Major { get; set; }

        // Professor fields
        public string? Department { get; set; }
        public string? Position { get; set; }

        // Staff fields
        public string? StaffId { get; set; }
        public string? OfficeLocation { get; set; }

                                                                                                                                                
        // Navigation properties
        public ICollection<Reports> SubmittedReports { get; set; }
    }
}