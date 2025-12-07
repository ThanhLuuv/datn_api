using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PromotionController : ControllerBase
{
    private readonly IPromotionService _promotionService;
    private readonly ILogger<PromotionController> _logger;

    public PromotionController(IPromotionService promotionService, ILogger<PromotionController> logger)
    {
        _promotionService = promotionService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách khuyến mãi với tìm kiếm và phân trang
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<PromotionListResponse>>> GetPromotions([FromQuery] PromotionSearchRequest request)
    {
        try
        {
            var result = await _promotionService.GetPromotionsAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPromotions");
            return StatusCode(500, new ApiResponse<PromotionListResponse> { Success = false, Message = "Lỗi server" });
        }
    }

    /// <summary>
    /// Lấy thông tin chi tiết khuyến mãi theo ID
    /// </summary>
    [HttpGet("{promotionId}")]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<PromotionDto>>> GetPromotionById(long promotionId)
    {
        try
        {
            var result = await _promotionService.GetPromotionByIdAsync(promotionId);
            
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPromotionById for ID {PromotionId}", promotionId);
            return StatusCode(500, new ApiResponse<PromotionDto> { Success = false, Message = "Lỗi server" });
        }
    }

    /// <summary>
    /// Tạo khuyến mãi mới
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<PromotionDto>>> CreatePromotion([FromBody] CreatePromotionDto createPromotionDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new ApiResponse<PromotionDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
            }

            // Get user email from JWT token
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value 
                           ?? User.FindFirst("email")?.Value 
                           ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized(new ApiResponse<PromotionDto> { Success = false, Message = "Không thể xác định người dùng" });
            }

            var result = await _promotionService.CreatePromotionAsync(createPromotionDto, userEmail);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return CreatedAtAction(nameof(GetPromotionById), new { promotionId = result.Data!.PromotionId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreatePromotion");
            return StatusCode(500, new ApiResponse<PromotionDto> { Success = false, Message = "Lỗi server" });
        }
    }

    /// <summary>
    /// Cập nhật thông tin khuyến mãi
    /// </summary>
    [HttpPut("{promotionId}")]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<PromotionDto>>> UpdatePromotion(long promotionId, [FromBody] UpdatePromotionDto updatePromotionDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new ApiResponse<PromotionDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
            }

            // Get user email from JWT token
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value 
                           ?? User.FindFirst("email")?.Value 
                           ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized(new ApiResponse<PromotionDto> { Success = false, Message = "Không thể xác định người dùng" });
            }

            var result = await _promotionService.UpdatePromotionAsync(promotionId, updatePromotionDto, userEmail);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdatePromotion for ID {PromotionId}", promotionId);
            return StatusCode(500, new ApiResponse<PromotionDto> { Success = false, Message = "Lỗi server" });
        }
    }

    /// <summary>
    /// Xóa khuyến mãi
    /// </summary>
    [HttpDelete("{promotionId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> DeletePromotion(long promotionId)
    {
        try
        {
            // Get user email from JWT token
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value 
                           ?? User.FindFirst("email")?.Value 
                           ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized(new ApiResponse<bool> { Success = false, Message = "Không thể xác định người dùng" });
            }

            var result = await _promotionService.DeletePromotionAsync(promotionId, userEmail);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeletePromotion for ID {PromotionId}", promotionId);
            return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Lỗi server" });
        }
    }

    /// <summary>
    /// Lấy thống kê khuyến mãi
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<PromotionStatsDto>>> GetPromotionStats()
    {
        try
        {
            var result = await _promotionService.GetPromotionStatsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPromotionStats");
            return StatusCode(500, new ApiResponse<PromotionStatsDto> { Success = false, Message = "Lỗi server" });
        }
    }

    /// <summary>
    /// Lấy danh sách sách đang có khuyến mãi
    /// </summary>
    [HttpGet("active-books")]
    [AllowAnonymous] // Cho phép khách hàng xem sách khuyến mãi
    public async Task<ActionResult<ApiResponse<List<PromotionBookDto>>>> GetActivePromotionBooks()
    {
        try
        {
            var result = await _promotionService.GetActivePromotionBooksAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetActivePromotionBooks");
            return StatusCode(500, new ApiResponse<List<PromotionBookDto>> { Success = false, Message = "Lỗi server" });
        }
    }

    /// <summary>
    /// Lấy danh sách khuyến mãi đang áp dụng cho một cuốn sách
    /// </summary>
    [HttpGet("book/{isbn}")]
    [AllowAnonymous] // Cho phép khách hàng xem khuyến mãi của sách
    public async Task<ActionResult<ApiResponse<List<PromotionDto>>>> GetActivePromotionsForBook(string isbn)
    {
        try
        {
            var result = await _promotionService.GetActivePromotionsForBookAsync(isbn);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetActivePromotionsForBook for ISBN {Isbn}", isbn);
            return StatusCode(500, new ApiResponse<List<PromotionDto>> { Success = false, Message = "Lỗi server" });
        }
    }
}
