using System.Collections.Generic;

namespace UnivMate.ViewModels
{
    public class UserReportsViewModel
    {
        public string UserName { get; set; }
        public List<ReportItemViewModel> Reports { get; set; }
    }

    public class ReportItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string StatusClass => Status.ToLower();
        public string SubmittedDate { get; set; }
        public string ResolvedDate { get; set; }
        public string Location { get; set; }
    }
}