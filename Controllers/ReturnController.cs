using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

/// <summary>
/// API quản lý trả hàng cho ADMIN và EMPLOYEE
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "PERM_READ_RETURN")]
public class ReturnController : ControllerBase
{
    private readonly IReturnService _returnService;

    public ReturnController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    /// <summary>
    /// ADMIN/EMPLOYEE tạo yêu cầu trả hàng cho khách hàng
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "PERM_WRITE_RETURN")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> CreateReturn([FromBody] CreateReturnDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ApiResponse<ReturnDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
        }

        var result = await _returnService.CreateReturnAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// ADMIN/EMPLOYEE xem tất cả yêu cầu trả hàng
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<ReturnListResponse>>> GetReturns([FromQuery] ReturnSearchRequest request)
    {
        var result = await _returnService.GetReturnsAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// ADMIN/EMPLOYEE xem chi tiết yêu cầu trả hàng
    /// </summary>
    [HttpGet("{returnId}")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> GetReturn(long returnId)
    {
        var result = await _returnService.GetReturnByIdAsync(returnId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// ADMIN/EMPLOYEE cập nhật trạng thái yêu cầu trả hàng
    /// </summary>
    [HttpPut("{returnId}/status")]
    [Authorize(Policy = "PERM_WRITE_RETURN")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> UpdateReturnStatus(
        long returnId, 
        [FromBody] UpdateReturnStatusRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ApiResponse<ReturnDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
        }

        // Lấy email từ token JWT
        var processorEmail = User.Identity?.Name;
        if (string.IsNullOrEmpty(processorEmail))
        {
            return Unauthorized(new ApiResponse<ReturnDto> { Success = false, Message = "Không xác định được người xử lý" });
        }

        var result = await _returnService.UpdateReturnStatusAsync(returnId, request, processorEmail);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}


