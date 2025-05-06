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
        public string ImagePath { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNotes { get; set; }

        // Relationship to User who submitted the report
        public int UserId { get; set; }
        public User User { get; set; }

        // Optional: Staff who resolved the report
        public int? ResolvedById { get; set; }
        public User? ResolvedBy { get; set; }

        public int? AssignedToId { get; set; }
        public User? AssignedTo { get; set; }
        public DateTime? AssignedAt { get; set; }

        public ICollection<ReportStatusHistory> StatusHistory { get; set; } = new List<ReportStatusHistory>();

    }
}