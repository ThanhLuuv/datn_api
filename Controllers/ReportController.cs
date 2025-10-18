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

    /// <summary>
    /// Báo cáo doanh thu theo tháng trong khoảng thời gian
    /// </summary>
    [HttpGet("revenue-monthly")]
    public async Task<ActionResult<ApiResponse<MonthlyRevenueReportResponse>>> GetMonthlyRevenue([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetMonthlyRevenueAsync(fromDate, toDate);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Báo cáo tồn kho tại ngày (ADMIN)
    /// </summary>
    [HttpGet("inventory")]
    public async Task<ActionResult<ApiResponse<InventoryReportResponse>>> GetInventory([FromQuery] DateTime date)
    {
        var result = await _reportService.GetInventoryAsOfDateAsync(date);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Tính lại giá nhập bình quân theo 4 phiếu nhập gần nhất (ADMIN)
    /// </summary>
    [HttpPost("books/{isbn}/recompute-average-price")]
    public async Task<ActionResult<ApiResponse<RecomputeAveragePriceResponse>>> RecomputeAveragePrice(string isbn)
    {
        var result = await _reportService.RecomputeAveragePriceAsync(isbn);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }
    /// <summary>
    /// Báo cáo doanh thu theo quý trong khoảng thời gian
    /// </summary>
    [HttpGet("revenue-quarterly")]
    public async Task<ActionResult<ApiResponse<QuarterlyRevenueReportResponse>>> GetQuarterlyRevenue([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetQuarterlyRevenueAsync(fromDate, toDate);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }
}


