using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SALES_EMPLOYEE,DELIVERY_EMPLOYEE,ADMIN")]
public class PurchaseOrderController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrderController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    /// <summary>
    /// Lấy danh sách đơn đặt mua với tìm kiếm và phân trang
    /// </summary>
    /// <param name="searchRequest">Tham số tìm kiếm</param>
    /// <returns>Danh sách đơn đặt mua</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PurchaseOrderListResponse>>> GetPurchaseOrders([FromQuery] PurchaseOrderSearchRequest searchRequest)
    {
        var result = await _purchaseOrderService.GetPurchaseOrdersAsync(searchRequest);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Lấy thông tin đơn đặt mua theo ID
    /// </summary>
    /// <param name="poId">ID đơn đặt mua</param>
    /// <returns>Thông tin đơn đặt mua</returns>
    [HttpGet("{poId}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> GetPurchaseOrder(long poId)
    {
        var result = await _purchaseOrderService.GetPurchaseOrderByIdAsync(poId);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }

    /// <summary>
    /// Tạo đơn đặt mua mới
    /// </summary>
    /// <param name="createPurchaseOrderDto">Thông tin đơn đặt mua mới</param>
    /// <returns>Thông tin đơn đặt mua đã tạo</returns>
    [HttpPost]
    [Authorize(Roles = "SALES_EMPLOYEE,ADMIN")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> CreatePurchaseOrder([FromBody] CreatePurchaseOrderDto createPurchaseOrderDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<PurchaseOrderDto>
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
            return Unauthorized(new ApiResponse<PurchaseOrderDto>
            {
                Success = false,
                Message = "Không thể xác định người dùng",
                Errors = new List<string> { "Token không hợp lệ" }
            });
        }

        var result = await _purchaseOrderService.CreatePurchaseOrderAsync(createPurchaseOrderDto, userId);
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetPurchaseOrder), new { poId = result.Data!.PoId }, result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Cập nhật đơn đặt mua
    /// </summary>
    /// <param name="poId">ID đơn đặt mua</param>
    /// <param name="updatePurchaseOrderDto">Thông tin cập nhật</param>
    /// <returns>Thông tin đơn đặt mua đã cập nhật</returns>
    [HttpPut("{poId}")]
    [Authorize(Roles = "SALES_EMPLOYEE,ADMIN")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> UpdatePurchaseOrder(long poId, [FromBody] UpdatePurchaseOrderDto updatePurchaseOrderDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<PurchaseOrderDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _purchaseOrderService.UpdatePurchaseOrderAsync(poId, updatePurchaseOrderDto);
        
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
    /// Xóa đơn đặt mua
    /// </summary>
    /// <param name="poId">ID đơn đặt mua</param>
    /// <returns>Kết quả xóa</returns>
    [HttpDelete("{poId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeletePurchaseOrder(long poId)
    {
        var result = await _purchaseOrderService.DeletePurchaseOrderAsync(poId);
        
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
}
