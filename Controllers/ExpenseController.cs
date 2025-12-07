using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpenseController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Tạo phiếu chi mới
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "PERM_WRITE_EXPENSE")]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherDto>>> CreateExpenseVoucher([FromBody] CreateExpenseVoucherDto request)
    {
        var userId = GetCurrentUserId();
        var result = await _expenseService.CreateExpenseVoucherAsync(request, userId);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Lấy danh sách phiếu chi
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "PERM_READ_EXPENSE")]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherResponse>>> GetExpenseVouchers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? expenseType = null)
    {
        var result = await _expenseService.GetExpenseVouchersAsync(pageNumber, pageSize, status, expenseType);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Lấy thông tin phiếu chi theo ID
    /// </summary>
    [HttpGet("{expenseVoucherId}")]
    [Authorize(Policy = "PERM_READ_EXPENSE")]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherDto>>> GetExpenseVoucherById(long expenseVoucherId)
    {
        var result = await _expenseService.GetExpenseVoucherByIdAsync(expenseVoucherId);
        if (result.Success) return Ok(result);
        return NotFound(result);
    }

    /// <summary>
    /// Duyệt phiếu chi
    /// </summary>
    [HttpPost("approve")]
    [Authorize(Policy = "PERM_WRITE_EXPENSE")]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherDto>>> ApproveExpenseVoucher([FromBody] ApproveExpenseVoucherDto request)
    {
        var userId = GetCurrentUserId();
        var result = await _expenseService.ApproveExpenseVoucherAsync(request, userId);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Từ chối phiếu chi
    /// </summary>
    [HttpPost("reject")]
    [Authorize(Policy = "PERM_WRITE_EXPENSE")]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherDto>>> RejectExpenseVoucher([FromBody] RejectExpenseVoucherDto request)
    {
        var userId = GetCurrentUserId();
        var result = await _expenseService.RejectExpenseVoucherAsync(request, userId);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    private long GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id");
        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out long userId))
        {
            return userId;
        }
        throw new UnauthorizedAccessException("Không thể xác định người dùng");
    }
}
