using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IAiSearchService _aiSearchService;
    private readonly ITextToSqlService _textToSqlService;

    public AiController(
        IAiService aiService,
        IAiSearchService aiSearchService,
        ITextToSqlService textToSqlService)
    {
        _aiService = aiService;
        _aiSearchService = aiSearchService;
        _textToSqlService = textToSqlService;
    }

    /// <summary>
    /// Gợi ý sách cho khách hàng bằng AI (GPT‑4o) dựa trên yêu cầu tự nhiên.
    /// </summary>
    [HttpPost("recommend-books")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AiBookRecommendationResponse>>> RecommendBooks(
        [FromBody] AiBookRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<AiBookRecommendationResponse>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _aiService.GetBookRecommendationsAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Trợ lý AI cho admin: phân tích mặt hàng bán chạy, gợi ý danh mục và sách nên nhập thêm / nhập mới.
    /// </summary>
    [HttpPost("admin-assistant")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<AdminAiAssistantResponse>>> AdminAssistant(
        [FromBody] AdminAiAssistantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _aiService.GetAdminInsightsAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Chatbox AI realtime cho admin (text) - trả lời các câu hỏi về doanh thu, tồn kho, đơn hàng...
    /// </summary>
    [HttpPost("admin-chat")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<AdminAiChatResponse>>> AdminChat(
        [FromBody] AdminAiChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<AdminAiChatResponse>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _aiService.GetAdminChatAnswerAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Voice assistant: admin gửi audio, hệ thống phân tích và trả về transcript + audio trả lời đã dựa trên dữ liệu thật.
    /// </summary>
    [HttpPost("admin-voice")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<AdminAiVoiceResponse>>> AdminVoice(
        [FromBody] AdminAiVoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<AdminAiVoiceResponse>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _aiService.GetAdminVoiceAnswerAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Nhập mới sách từ dữ liệu do AI đề xuất (admin có thể chỉnh sửa trước trên UI).
    /// </summary>
    [HttpPost("admin-import-book")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<AdminAiImportBookResponse>>> AdminImportBook(
        [FromBody] AdminAiImportBookRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<AdminAiImportBookResponse>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _aiService.ImportAiSuggestedBookAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// AI search (RAG) trả lời câu hỏi dựa trên dữ liệu nội bộ đã index.
    /// </summary>
    [HttpPost("search")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<AiSearchResponse>>> SearchKnowledgeBase(
        [FromBody] AiSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _aiSearchService.SearchKnowledgeBaseAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Rebuild index cho AI search (chỉ Admin).
    /// </summary>
    [HttpPost("search/reindex")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<AiSearchReindexResponse>>> RebuildKnowledgeBase(
        [FromBody] AiSearchReindexRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _aiSearchService.RebuildAiSearchIndexAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Chat với AI Assistant sử dụng Hybrid Architecture (Function Calling + RAG).
    /// AI tự động quyết định dùng RAG (cho sách) hoặc Function Calling (cho đơn hàng, hóa đơn).
    /// </summary>
    [HttpPost("chat")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<AiSearchResponse>>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Query không được để trống",
                Errors = new List<string> { "Query is required" }
            });
        }

        var result = await _aiSearchService.ChatWithAssistantAsync(request.Query, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Text-to-SQL assistant (RAG) chuyển câu hỏi tự nhiên thành SQL và trả lời dựa trên dữ liệu thực tế.
    /// </summary>
    [HttpPost("text-to-sql")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TextToSqlResponse>>> TextToSql(
        [FromBody] TextToSqlRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<TextToSqlResponse>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _textToSqlService.AskAsync(request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }
}

public class ChatRequest
{
    public string Query { get; set; } = string.Empty;
}







