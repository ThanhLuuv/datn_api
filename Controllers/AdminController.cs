using BookStore.Api.DTOs;
using BookStore.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/admin")] 
[Authorize(Roles = "ADMIN,SALES_EMPLOYEE")]
public class AdminController : ControllerBase
{
	private readonly BookStoreDbContext _context;

	public AdminController(BookStoreDbContext context)
	{
		_context = context;
	}

	[HttpGet("dashboard/summary")]
	public async Task<ActionResult<ApiResponse<DashboardSummaryResponse>>> GetDashboardSummary()
	{
		var totalBooks = await _context.Books.CountAsync();
		var resp = new DashboardSummaryResponse
		{
			Summary = new DashboardSummaryDto { TotalBooks = totalBooks }
		};
		return Ok(new ApiResponse<DashboardSummaryResponse>
		{
			Success = true,
			Message = "OK",
			Data = resp
		});
	}

	[HttpGet("dashboard/total-users")]
	public async Task<ActionResult<ApiResponse<object>>> GetTotalUsers()
	{
		var total = await _context.Accounts.CountAsync();
		return Ok(new ApiResponse<object>
		{
			Success = true,
			Message = "OK",
			Data = new { totalUsers = total }
		});
	}

	[HttpGet("dashboard/orders-today")]
	public async Task<ActionResult<ApiResponse<object>>> GetOrdersToday()
	{
		var today = DateTime.UtcNow.Date;
		var count = await _context.Orders
			.Where(o => o.PlacedAt >= today && o.PlacedAt < today.AddDays(1))
			.CountAsync();
		return Ok(new ApiResponse<object>
		{
			Success = true,
			Message = "OK",
			Data = new { totalOrdersToday = count }
		});
	}
}


