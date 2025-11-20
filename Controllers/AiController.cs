using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}





