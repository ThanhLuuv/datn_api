using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IPromotionService
{
    Task<ApiResponse<PromotionListResponse>> GetPromotionsAsync(PromotionSearchRequest request);
    Task<ApiResponse<PromotionDto>> GetPromotionByIdAsync(long promotionId);
    Task<ApiResponse<PromotionDto>> CreatePromotionAsync(CreatePromotionDto createPromotionDto, string createdByEmail);
    Task<ApiResponse<PromotionDto>> UpdatePromotionAsync(long promotionId, UpdatePromotionDto updatePromotionDto, string updatedByEmail);
    Task<ApiResponse<bool>> DeletePromotionAsync(long promotionId, string deletedByEmail);
    Task<ApiResponse<PromotionStatsDto>> GetPromotionStatsAsync();
    Task<ApiResponse<List<PromotionBookDto>>> GetActivePromotionBooksAsync();
    Task<ApiResponse<List<PromotionDto>>> GetActivePromotionsForBookAsync(string isbn);
}
