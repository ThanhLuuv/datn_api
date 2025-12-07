using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "PERM_READ_CART")]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly ILogger<CartController> _logger;

    public CartController(ICartService cartService, ILogger<CartController> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy giỏ hàng của khách hàng
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CartDto>>> GetCart()
    {
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<CartDto>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing customerId" }
            });
        }

        var result = await _cartService.GetCartAsync(customerId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Lấy tóm tắt giỏ hàng (số lượng, tổng tiền)
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<CartSummaryDto>>> GetCartSummary()
    {
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<CartSummaryDto>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing customerId" }
            });
        }

        var result = await _cartService.GetCartSummaryAsync(customerId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Thêm sách vào giỏ hàng
    /// </summary>
    [HttpPost("add")]
    [Authorize(Policy = "PERM_WRITE_CART")]
    public async Task<ActionResult<ApiResponse<CartItemDto>>> AddToCart([FromBody] AddToCartRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<CartItemDto>
            {
                Success = false,
                Message = "Invalid data",
                Errors = errors
            });
        }

        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<CartItemDto>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing customerId" }
            });
        }

        var result = await _cartService.AddToCartAsync(customerId.Value, request);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Cập nhật số lượng sách trong giỏ hàng
    /// </summary>
    [HttpPut("{cartItemId}")]
    [Authorize(Policy = "PERM_WRITE_CART")]
    public async Task<ActionResult<ApiResponse<CartItemDto>>> UpdateCartItem(long cartItemId, [FromBody] UpdateCartItemRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<CartItemDto>
            {
                Success = false,
                Message = "Invalid data",
                Errors = errors
            });
        }

        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<CartItemDto>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing customerId" }
            });
        }

        var result = await _cartService.UpdateCartItemAsync(customerId.Value, cartItemId, request);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Xóa một sản phẩm khỏi giỏ hàng theo cartItemId
    /// </summary>
    [HttpDelete("{cartItemId}")]
    [Authorize(Policy = "PERM_WRITE_CART")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveFromCart(long cartItemId)
    {
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<bool>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing customerId" }
            });
        }

        var result = await _cartService.RemoveFromCartAsync(customerId.Value, cartItemId);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Xóa tất cả sản phẩm khỏi giỏ hàng
    /// </summary>
    [HttpDelete("clear")]
    [Authorize(Policy = "PERM_WRITE_CART")]
    public async Task<ActionResult<ApiResponse<bool>>> ClearCart()
    {
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<bool>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing customerId" }
            });
        }

        var result = await _cartService.ClearCartAsync(customerId.Value);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Xóa tất cả sản phẩm của một sách khỏi giỏ hàng theo ISBN
    /// </summary>
    [HttpDelete("book/{isbn}")]
    [Authorize(Policy = "PERM_WRITE_CART")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveBookFromCart(string isbn)
    {
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<bool>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing customerId" }
            });
        }

        var result = await _cartService.RemoveBookFromCartAsync(customerId.Value, isbn);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    private async Task<long?> GetCustomerIdFromToken()
    {
        try
        {
            // Find the nameidentifier claim that contains a numeric value (accountId)
            var accountIdClaim = User.Claims
                .Where(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                .FirstOrDefault(c => long.TryParse(c.Value, out _))?.Value;
                
            if (string.IsNullOrEmpty(accountIdClaim) || !long.TryParse(accountIdClaim, out var accountId))
            {
                return null;
            }

            // Find customer by accountId
            // Note: This is a simplified approach. In production, you might want to cache this or use a different approach
            var customer = await _cartService.GetCustomerByAccountIdAsync(accountId);
            return customer?.CustomerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer ID from token");
            return null;
        }
    }
}
