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


