using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UnivMate.Models
{
    public class Reports
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string LocationGroup { get; set; }  // Add this
        public string LocationSubgroup { get; set; }  // Add this
        public string ImagePath { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNotes { get; set; }
        public string? AdminComment { get; set; } // New property for staff comments
        public int? DaysSinceLastAdminResponse { get; set; } // Add this
        public bool NeedsAttention => DaysSinceLastAdminResponse >= 7;


        // Relationship to User who submitted the report
        public int UserId { get; set; }
        public User User { get; set; }

        // Optional: Staff who resolved the report
        public int? ResolvedById { get; set; }
        public User? ResolvedBy { get; set; }

        public int? AssignedToId { get; set; }
        public User? AssignedTo { get; set; }
        public DateTime? AssignedAt { get; set; }
        public int? Rating { get; set; } // 1-5 star rating
        public DateTime? RatedAt { get; set; }

        public ICollection<ReportStatusHistory> StatusHistory { get; set; } = new List<ReportStatusHistory>();
        public ICollection<ReportComment> Comments { get; set; } = new List<ReportComment>();
    }

    public class ReportComment
    {
        public int Id { get; set; }

        [Required]
        public string Content { get; set; }

        public string ImagePath { get; set; }
       
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Foreign key to Reports
        public int ReportId { get; set; }
        public Reports Report { get; set; }

        // Foreign key to User who made the comment
        public int AuthorId { get; set; }
        public User Author { get; set; }
    }
}
