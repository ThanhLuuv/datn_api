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

    /// <summary>
    /// Chatbox AI dạng live cho admin: trả lời câu hỏi về doanh thu, tồn kho, đơn hàng... dựa trên dữ liệu thật.
    /// </summary>
    Task<ApiResponse<AdminAiChatResponse>> GetAdminChatAnswerAsync(AdminAiChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voice assistant: nhận audio đầu vào, tự truy xuất dữ liệu hệ thống và trả về transcript + audio trả lời.
    /// </summary>
    Task<ApiResponse<AdminAiVoiceResponse>> GetAdminVoiceAnswerAsync(AdminAiVoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhập nhanh sách mới từ gợi ý AI (có thể chỉnh sửa trước khi lưu).
    /// </summary>
    Task<ApiResponse<AdminAiImportBookResponse>> ImportAiSuggestedBookAsync(AdminAiImportBookRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm kiến thức nội bộ bằng AI Search (RAG).
    /// </summary>
    Task<ApiResponse<AiSearchResponse>> SearchKnowledgeBaseAsync(AiSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuild / cập nhật index cho AI Search.
    /// </summary>
    Task<ApiResponse<AiSearchReindexResponse>> RebuildAiSearchIndexAsync(AiSearchReindexRequest request, CancellationToken cancellationToken = default);
}







