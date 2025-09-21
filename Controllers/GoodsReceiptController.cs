using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "DELIVERY_EMPLOYEE,ADMIN")]
public class GoodsReceiptController : ControllerBase
{
    private readonly IGoodsReceiptService _goodsReceiptService;

    public GoodsReceiptController(IGoodsReceiptService goodsReceiptService)
    {
        _goodsReceiptService = goodsReceiptService;
    }

    /// <summary>
    /// Lấy danh sách phiếu nhập với tìm kiếm và phân trang
    /// </summary>
    /// <param name="searchRequest">Tham số tìm kiếm</param>
    /// <returns>Danh sách phiếu nhập</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<GoodsReceiptListResponse>>> GetGoodsReceipts([FromQuery] GoodsReceiptSearchRequest searchRequest)
    {
        var result = await _goodsReceiptService.GetGoodsReceiptsAsync(searchRequest);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Lấy thông tin phiếu nhập theo ID
    /// </summary>
    /// <param name="grId">ID phiếu nhập</param>
    /// <returns>Thông tin phiếu nhập</returns>
    [HttpGet("{grId}")]
    public async Task<ActionResult<ApiResponse<GoodsReceiptDto>>> GetGoodsReceipt(long grId)
    {
        var result = await _goodsReceiptService.GetGoodsReceiptByIdAsync(grId);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }

    /// <summary>
    /// Tạo phiếu nhập mới
    /// </summary>
    /// <param name="createGoodsReceiptDto">Thông tin phiếu nhập mới</param>
    /// <returns>Thông tin phiếu nhập đã tạo</returns>
    [HttpPost]
    [Authorize(Roles = "DELIVERY_EMPLOYEE,ADMIN")]
    public async Task<ActionResult<ApiResponse<GoodsReceiptDto>>> CreateGoodsReceipt([FromBody] CreateGoodsReceiptDto createGoodsReceiptDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<GoodsReceiptDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        // Get current user ID from JWT token
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
        {
            return Unauthorized(new ApiResponse<GoodsReceiptDto>
            {
                Success = false,
                Message = "Không thể xác định người dùng",
                Errors = new List<string> { "Token không hợp lệ" }
            });
        }

        var result = await _goodsReceiptService.CreateGoodsReceiptAsync(createGoodsReceiptDto, userId);
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetGoodsReceipt), new { grId = result.Data!.GrId }, result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Cập nhật phiếu nhập
    /// </summary>
    /// <param name="grId">ID phiếu nhập</param>
    /// <param name="updateGoodsReceiptDto">Thông tin cập nhật</param>
    /// <returns>Thông tin phiếu nhập đã cập nhật</returns>
    [HttpPut("{grId}")]
    [Authorize(Roles = "DELIVERY_EMPLOYEE,ADMIN")]
    public async Task<ActionResult<ApiResponse<GoodsReceiptDto>>> UpdateGoodsReceipt(long grId, [FromBody] UpdateGoodsReceiptDto updateGoodsReceiptDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<GoodsReceiptDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _goodsReceiptService.UpdateGoodsReceiptAsync(grId, updateGoodsReceiptDto);
        
        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message.Contains("Không tìm thấy"))
        {
            return NotFound(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Xóa phiếu nhập
    /// </summary>
    /// <param name="grId">ID phiếu nhập</param>
    /// <returns>Kết quả xóa</returns>
    [HttpDelete("{grId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteGoodsReceipt(long grId)
    {
        var result = await _goodsReceiptService.DeleteGoodsReceiptAsync(grId);
        
        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message.Contains("Không tìm thấy"))
        {
            return NotFound(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Lấy danh sách đơn đặt mua có thể tạo phiếu nhập
    /// </summary>
    /// <returns>Danh sách đơn đặt mua</returns>
    [HttpGet("available-purchase-orders")]
    public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetAvailablePurchaseOrders()
    {
        var result = await _goodsReceiptService.GetAvailablePurchaseOrdersAsync();
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }
}
