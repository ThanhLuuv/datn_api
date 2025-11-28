using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IAiSearchService
{
    /// <summary>
    /// Tìm kiếm kiến thức nội bộ bằng AI Search (RAG).
    /// </summary>
    Task<ApiResponse<AiSearchResponse>> SearchKnowledgeBaseAsync(AiSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuild / cập nhật index cho AI Search.
    /// </summary>
    Task<ApiResponse<AiSearchReindexResponse>> RebuildAiSearchIndexAsync(AiSearchReindexRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chat với AI Assistant sử dụng Hybrid Architecture (Function Calling + RAG).
    /// </summary>
    Task<ApiResponse<AiSearchResponse>> ChatWithAssistantAsync(string userQuery, CancellationToken cancellationToken = default);
}
