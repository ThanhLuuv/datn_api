using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IGoodsReceiptService
{
    Task<ApiResponse<GoodsReceiptListResponse>> GetGoodsReceiptsAsync(GoodsReceiptSearchRequest searchRequest);
    Task<ApiResponse<GoodsReceiptDto>> GetGoodsReceiptByIdAsync(long grId);
    Task<ApiResponse<GoodsReceiptDto>> CreateGoodsReceiptAsync(CreateGoodsReceiptDto createGoodsReceiptDto, long createdBy);
    Task<ApiResponse<GoodsReceiptDto>> UpdateGoodsReceiptAsync(long grId, UpdateGoodsReceiptDto updateGoodsReceiptDto);
    Task<ApiResponse<bool>> DeleteGoodsReceiptAsync(long grId);
    Task<ApiResponse<List<PurchaseOrderDto>>> GetAvailablePurchaseOrdersAsync();
}
