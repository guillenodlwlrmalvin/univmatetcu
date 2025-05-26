// Models/Notification.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace UnivMate.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }  // The user who should receive this notification

        [Required]
        [StringLength(200)]
        public string Message { get; set; }  // Short summary (e.g., "Your report was accepted")

        public string Details { get; set; }   // Longer details or comment

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        // Navigation property
        public User User { get; set; }
        public int? ReportId { get; set; } // Add this line
        public Reports Report { get; set; } // Add this line
    }
}