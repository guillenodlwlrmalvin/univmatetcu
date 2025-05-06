namespace UnivMate.Models
{
    // Add to your Models folder
    public class ReportStatusHistory
    {
        public int Id { get; set; }
        public int ReportId { get; set; }
        public Reports Report { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Notes { get; set; }
    }
}
