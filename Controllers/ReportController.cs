using BookStore.Api.DTOs;
using BookStore.Api.Services;
using BookStore.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")] // báo cáo cho admin
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly BookStoreDbContext _context;

    public ReportController(IReportService reportService, BookStoreDbContext context)
	{
        _reportService = reportService;
        _context = context;
	}

	/// <summary>
	/// Báo cáo doanh thu theo ngày trong khoảng thời gian
	/// </summary>
	[HttpGet("revenue")]
	public async Task<ActionResult<ApiResponse<RevenueReportResponse>>> GetRevenue([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
	{
		var req = new RevenueReportRequest { FromDate = fromDate, ToDate = toDate };
		var result = await _reportService.GetRevenueByDateRangeAsync(req);
        if (result.Success)
        {
            result.Data!.GeneratedBy = BuildGeneratedBy();
            return Ok(result);
        }
		return BadRequest(result);
	}

    /// <summary>
    /// Báo cáo doanh thu theo tháng trong khoảng thời gian
    /// </summary>
    [HttpGet("revenue-monthly")]
    public async Task<ActionResult<ApiResponse<MonthlyRevenueReportResponse>>> GetMonthlyRevenue([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _reportService.GetMonthlyRevenueAsync(fromDate, toDate);
        if (result.Success)
        {
            result.Data!.GeneratedBy = BuildGeneratedBy();
            return Ok(result);
        }
        return BadRequest(result);
    }

    /// <summary>
    /// Báo cáo tồn kho tại ngày (ADMIN)
    /// </summary>
    [HttpGet("inventory")]
    public async Task<ActionResult<ApiResponse<InventoryReportResponse>>> GetInventory([FromQuery] DateTime date)
    {
        var result = await _reportService.GetInventoryAsOfDateAsync(date);
        if (result.Success)
        {
            result.Data!.GeneratedBy = BuildGeneratedBy();
            return Ok(result);
        }
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
        if (result.Success)
        {
            result.Data!.GeneratedBy = BuildGeneratedBy();
            return Ok(result);
        }
        return BadRequest(result);
    }

    private ReportGeneratedByDto BuildGeneratedBy()
    {
        long? accountId = null;
        var idClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
        if (idClaim != null && long.TryParse(idClaim.Value, out var id)) accountId = id;

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value;
        var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? User.FindFirst("name")?.Value;

        // Fallback: look up by email if claims missing
        if ((!accountId.HasValue || string.IsNullOrWhiteSpace(name)) && !string.IsNullOrWhiteSpace(email))
        {
            var account = _context.Accounts
                .Include(a => a.Employee)
                .FirstOrDefault(a => a.Email == email);
            if (account != null)
            {
                if (!accountId.HasValue) accountId = account.AccountId;
                if (string.IsNullOrWhiteSpace(name)) name = account.Employee?.FullName;
            }
        }

        return new ReportGeneratedByDto
        {
            AccountId = accountId,
            Email = email,
            FullName = name
        };
    }
}


