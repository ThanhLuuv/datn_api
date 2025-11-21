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

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
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
}







