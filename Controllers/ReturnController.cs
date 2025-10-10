using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,EMPLOYEE")]
public class ReturnController : ControllerBase
{
    private readonly IReturnService _returnService;

    public ReturnController(IReturnService returnService)
    {
        _returnService = returnService;
    }

    [HttpPost]
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

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ReturnListResponse>>> GetReturns([FromQuery] ReturnSearchRequest request)
    {
        var result = await _returnService.GetReturnsAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("{returnId}")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> GetReturn(long returnId)
    {
        var result = await _returnService.GetReturnByIdAsync(returnId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPut("{returnId}/status")]
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


