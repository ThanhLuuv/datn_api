using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IPurchaseOrderService
{
    Task<ApiResponse<PurchaseOrderListResponse>> GetPurchaseOrdersAsync(PurchaseOrderSearchRequest searchRequest);
    Task<ApiResponse<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(long poId);
    Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto createPurchaseOrderDto, long createdByAccountId);
    Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto createPurchaseOrderDto, string createdByEmail);
    Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderAsync(long poId, UpdatePurchaseOrderDto updatePurchaseOrderDto);
    Task<ApiResponse<bool>> DeletePurchaseOrderAsync(long poId);
    Task<ApiResponse<PurchaseOrderDto>> ChangeStatusAsync(long poId, ChangePurchaseOrderStatusDto request);
}
