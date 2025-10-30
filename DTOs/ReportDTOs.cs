using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class RevenueReportRequest
{
	[Required]
	public DateTime FromDate { get; set; }
	[Required]
	public DateTime ToDate { get; set; }
}

public class InventoryReportItem
{
    public string Category { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public decimal AveragePrice { get; set; }
}

public class InventoryReportResponse
{
    public DateTime ReportDate { get; set; }
    public List<InventoryReportItem> Items { get; set; } = new();
    public ReportGeneratedByDto? GeneratedBy { get; set; }
}

public class RecomputeAveragePriceResponse
{
    public string Isbn { get; set; } = string.Empty;
    public decimal AveragePrice { get; set; }
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
    public ReportGeneratedByDto? GeneratedBy { get; set; }
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
    public ReportGeneratedByDto? GeneratedBy { get; set; }
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
    public ReportGeneratedByDto? GeneratedBy { get; set; }
}

public class ReportGeneratedByDto
{
    public long? AccountId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
}


