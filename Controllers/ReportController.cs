using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")] // báo cáo cho admin
public class ReportController : ControllerBase
{
	private readonly IReportService _reportService;

	public ReportController(IReportService reportService)
	{
		_reportService = reportService;
	}

	/// <summary>
	/// Báo cáo doanh thu theo ngày trong khoảng thời gian
	/// </summary>
	[HttpGet("revenue")]
	public async Task<ActionResult<ApiResponse<RevenueReportResponse>>> GetRevenue([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
	{
		var req = new RevenueReportRequest { FromDate = fromDate, ToDate = toDate };
		var result = await _reportService.GetRevenueByDateRangeAsync(req);
		if (result.Success) return Ok(result);
		return BadRequest(result);
	}
}


