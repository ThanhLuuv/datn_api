using BookStore.Api.DTOs;
using BookStore.Api.Models;

namespace BookStore.Api.Services;

public interface ICartService
{
    Task<ApiResponse<CartDto>> GetCartAsync(long customerId);
    Task<ApiResponse<CartSummaryDto>> GetCartSummaryAsync(long customerId);
    Task<ApiResponse<CartItemDto>> AddToCartAsync(long customerId, AddToCartRequestDto request);
    Task<ApiResponse<CartItemDto>> UpdateCartItemAsync(long customerId, long cartItemId, UpdateCartItemRequestDto request);
    Task<ApiResponse<bool>> RemoveFromCartAsync(long customerId, long cartItemId);
    Task<ApiResponse<bool>> ClearCartAsync(long customerId);
    Task<ApiResponse<bool>> RemoveBookFromCartAsync(long customerId, string isbn);
    Task<Customer?> GetCustomerByAccountIdAsync(long accountId);
}
