using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IAiService
{
    /// <summary>
    /// Gợi ý sách cho khách hàng dựa trên yêu cầu tự do + dữ liệu sách trong hệ thống.
    /// </summary>
    Task<ApiResponse<AiBookRecommendationResponse>> GetBookRecommendationsAsync(AiBookRecommendationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trợ lý AI cho admin: phân tích mặt hàng bán chạy, danh mục, đánh giá khách hàng và gợi ý nhập hàng.
    /// </summary>
    Task<ApiResponse<AdminAiAssistantResponse>> GetAdminInsightsAsync(AdminAiAssistantRequest request, CancellationToken cancellationToken = default);
}







