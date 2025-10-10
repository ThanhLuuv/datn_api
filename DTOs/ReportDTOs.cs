using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class RevenueReportRequest
{
	[Required]
	public DateTime FromDate { get; set; }
	[Required]
	public DateTime ToDate { get; set; }
}

public class RevenueByDayDto
{
	public DateTime Day { get; set; }
	public decimal Revenue { get; set; }
}

public class RevenueReportResponse
{
	public List<RevenueByDayDto> Items { get; set; } = new();
	public decimal TotalRevenue { get; set; }
}


public class RevenueByMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
}

public class MonthlyRevenueReportResponse
{
    public List<RevenueByMonthDto> Items { get; set; } = new();
    public decimal TotalRevenue { get; set; }
}

public class RevenueByQuarterDto
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal Revenue { get; set; }
}

public class QuarterlyRevenueReportResponse
{
    public List<RevenueByQuarterDto> Items { get; set; } = new();
    public decimal TotalRevenue { get; set; }
}


