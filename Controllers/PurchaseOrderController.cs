using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BookStore.Api.Models;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // require auth by default
public class PurchaseOrderController : ControllerBase
{
	private readonly IPurchaseOrderService _purchaseOrderService;
	private readonly ILogger<PurchaseOrderController> _logger;

	public PurchaseOrderController(IPurchaseOrderService purchaseOrderService, ILogger<PurchaseOrderController> logger)
	{
		_purchaseOrderService = purchaseOrderService;
		_logger = logger;
	}
	/// <summary>
	/// Lấy danh sách đơn đặt mua với tìm kiếm và phân trang
	/// </summary>
	/// <param name="searchRequest">Tham số tìm kiếm</param>
	/// <returns>Danh sách đơn đặt mua</returns>
	[HttpGet]
	[Authorize(Policy = "PERM_READ_PO")]
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
	[Authorize(Policy = "PERM_READ_PO")]
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
	[Authorize(Policy = "PERM_WRITE_PO")]
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

		// Debug: Log all claims
		_logger.LogInformation("[CreatePO] User.Identity.IsAuthenticated: {Auth}", User.Identity?.IsAuthenticated);
		_logger.LogInformation("[CreatePO] Claims count: {Count}", User.Claims.Count());
		foreach (var claim in User.Claims)
		{
			_logger.LogInformation("[CreatePO] Claim: {Type} = {Value}", claim.Type, claim.Value);
		}

		// Lấy accountId từ claims - tìm claim NameIdentifier có giá trị là số
		var nameIdentifierClaims = User.Claims.Where(c => c.Type == ClaimTypes.NameIdentifier).ToList();
		_logger.LogInformation("[CreatePO] Found {Count} NameIdentifier claims", nameIdentifierClaims.Count);
		
		string? accountIdClaim = null;
		foreach (var claim in nameIdentifierClaims)
		{
			_logger.LogInformation("[CreatePO] NameIdentifier claim: {Value}", claim.Value);
			if (long.TryParse(claim.Value, out _))
			{
				accountIdClaim = claim.Value;
				break;
			}
		}
		
		if (string.IsNullOrEmpty(accountIdClaim) || !long.TryParse(accountIdClaim, out long accountId))
		{
			_logger.LogError("[CreatePO] Cannot get numeric accountId from token claims. Found claims: {Claims}", 
				string.Join(", ", nameIdentifierClaims.Select(c => c.Value)));
			return Unauthorized(new ApiResponse<PurchaseOrderDto>
			{
				Success = false,
				Message = "Không thể xác định người dùng từ token",
				Errors = new List<string> { "Token không hợp lệ hoặc thiếu thông tin accountId" }
			});
		}

		_logger.LogInformation("[CreatePO] Creating PO for accountId={AccountId}", accountId);

		var result = await _purchaseOrderService.CreatePurchaseOrderAsync(createPurchaseOrderDto, accountId);
		
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
	[Authorize(Policy = "PERM_WRITE_PO")]
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
	[Authorize(Policy = "PERM_WRITE_PO")]
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

	/// <summary>
	/// Đổi trạng thái đơn đặt mua
	/// </summary>
	/// <param name="poId">ID đơn đặt mua</param>
	/// <param name="request">Trạng thái mới</param>
	/// <returns>Đơn đặt mua sau khi cập nhật</returns>
	[HttpPost("{poId}/change-status")]
	[Authorize(Policy = "PERM_WRITE_PO")]
	public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> ChangeStatus(long poId, [FromBody] ChangePurchaseOrderStatusDto request)
	{
		_logger.LogInformation("[ChangeStatus] poId={PoId}, newStatusId={Status}", poId, request.NewStatusId);
		var result = await _purchaseOrderService.ChangeStatusAsync(poId, request);
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
