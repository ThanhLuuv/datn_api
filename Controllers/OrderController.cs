using BookStore.Api.DTOs;
using BookStore.Api.Services;
using BookStore.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(IOrderService orderService, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVERY_EMPLOYEE")] 
    public async Task<ActionResult<ApiResponse<OrderListResponse>>> GetOrders([FromQuery] OrderSearchRequest request)
    {
        var result = await _orderService.GetOrdersAsync(request);
        return Ok(result);
    }

    [HttpGet("my-orders")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<ApiResponse<OrderListResponse>>> GetMyOrders([FromQuery] OrderSearchRequest request)
    {
        // Get account ID from token
        // Find the nameidentifier claim that contains a numeric value (accountId)
        var accountIdClaim = User.Claims
            .Where(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
            .FirstOrDefault(c => long.TryParse(c.Value, out _))?.Value;
            
        if (string.IsNullOrEmpty(accountIdClaim) || !long.TryParse(accountIdClaim, out var accountId))
        {
            return BadRequest(new ApiResponse<OrderListResponse>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing accountId" }
            });
        }

        // Find customer by accountId
        var customer = await _orderService.GetCustomerByAccountIdAsync(accountId);
        if (customer is null)
        {
            return BadRequest(new ApiResponse<OrderListResponse>
            {
                Success = false,
                Message = "Customer not found",
                Errors = new List<string> { "Customer profile not found" }
            });
        }

        // Set customer filter to only show orders for this customer
        request.CustomerId = customer!.CustomerId;
        var result = await _orderService.GetOrdersAsync(request);
        return Ok(result);
    }

    [HttpGet("my-assigned-orders")]
    [Authorize(Roles = "DELIVERY_EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<OrderListResponse>>> GetMyAssignedOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized(new ApiResponse<OrderListResponse> { Success = false, Message = "Không thể xác định người dùng" });

        // Map email -> employeeId
        var employeeId = await _orderService.GetEmployeeIdByEmailAsync(email);
        if (employeeId == null)
        {
            return Forbid();
        }

        var result = await _orderService.GetMyAssignedOrdersAsync(employeeId.Value, pageNumber, pageSize);
        return Ok(result);
    }

    [HttpGet("{orderId}")]
    [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVERY_EMPLOYEE")] 
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrder(long orderId)
    {
        var result = await _orderService.GetOrderByIdAsync(orderId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost("{orderId}/approve")]
    [Authorize(Roles = "ADMIN,EMPLOYEE")] 
    public async Task<ActionResult<ApiResponse<OrderDto>>> ApproveOrder(long orderId, [FromBody] ApproveOrderRequest request)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized(new ApiResponse<OrderDto> { Success = false, Message = "Không thể xác định người dùng" });
        var result = await _orderService.ApproveOrderAsync(orderId, request, email);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{orderId}/assign-delivery")]
    [Authorize(Roles = "ADMIN,EMPLOYEE")] 
    public async Task<ActionResult<ApiResponse<OrderDto>>> AssignDelivery(long orderId, [FromBody] AssignDeliveryRequest request)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized(new ApiResponse<OrderDto> { Success = false, Message = "Không thể xác định người dùng" });
        var result = await _orderService.AssignDeliveryAsync(orderId, request, email);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{orderId}/confirm-delivered")]
    [Authorize(Roles = "ADMIN,DELIVERY_EMPLOYEE")] 
    public async Task<ActionResult<ApiResponse<OrderDto>>> ConfirmDelivered(long orderId, [FromBody] ConfirmDeliveredRequest request)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized(new ApiResponse<OrderDto> { Success = false, Message = "Không thể xác định người dùng" });
        var result = await _orderService.ConfirmDeliveredAsync(orderId, request, email);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{orderId}/cancel")]
    [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVERY_EMPLOYEE,CUSTOMER")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> CancelOrder(long orderId, [FromBody] CancelOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<OrderDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized(new ApiResponse<OrderDto> { Success = false, Message = "Không thể xác định người dùng" });
        
        var result = await _orderService.CancelOrderAsync(orderId, request, email);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("{orderId}/delivery-candidates")]
    [Authorize(Roles = "ADMIN,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<List<SuggestedEmployeeDto>>>> GetDeliveryCandidates(long orderId)
    {
        var result = await _orderService.GetDeliveryCandidatesAsync(orderId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> CreateOrder([FromBody] CreateOrderDto createOrderDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<OrderDto>
            {
                Success = false,
                Message = "Invalid data",
                Errors = errors
            });
        }

        // Get customer ID from claims
        var nameIdentifierClaims = User.Claims.Where(c => c.Type == ClaimTypes.NameIdentifier).ToList();
        string? accountIdClaim = null;
        foreach (var claim in nameIdentifierClaims)
        {
            if (long.TryParse(claim.Value, out _))
            {
                accountIdClaim = claim.Value;
                break;
            }
        }
        
        if (string.IsNullOrEmpty(accountIdClaim) || !long.TryParse(accountIdClaim, out long accountId))
        {
            return Unauthorized(new ApiResponse<OrderDto>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing accountId" }
            });
        }

        var result = await _orderService.CreateOrderAsync(createOrderDto, accountId);
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetOrder), new { orderId = result.Data!.OrderId }, result);
        }

        return BadRequest(result);
    }
}


