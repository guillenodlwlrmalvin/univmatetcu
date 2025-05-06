using System.Collections.Generic;
using UnivMate.Models;

namespace UnivMate.ViewModels
{
    public class DashboardViewModel
    {
        public string UserName { get; set; }
        public ReportViewModel ReportViewModel { get; set; }
        public List<ReportItemViewModel> RecentReports { get; set; }
    }
}