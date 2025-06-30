// IReportService.cs
using Microsoft.EntityFrameworkCore;
using UnivMate.Data;

public interface IReportService
{
    Task<string> GetUserIdForReportAsync(int reportId);
}

// ReportService.cs
public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetUserIdForReportAsync(int reportId)
    {
        var report = await _context.Reports
            .Where(r => r.Id == reportId)
            .Select(r => r.UserId.ToString())
            .FirstOrDefaultAsync();

        return report;
    }
}