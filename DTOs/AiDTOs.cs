using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class AiBookRecommendationRequest
{
    /// <summary>
    /// Yêu cầu / nhu cầu của khách hàng (ví dụ: "muốn sách self-help về quản lý thời gian cho sinh viên")
    /// </summary>
    [Required]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Số sách tối đa cần gợi ý
    /// </summary>
    public int MaxResults { get; set; } = 12;
}

public class AiBookRecommendationResponse
{
    /// <summary>
    /// Danh sách sách được gợi ý (giống BookDto, có thêm tóm tắt từ AI nếu có)
    /// </summary>
    public List<BookDto> Books { get; set; } = new();

    /// <summary>
    /// Ghi chú / tóm tắt chung từ AI (tuỳ chọn)
    /// </summary>
    public string? Summary { get; set; }
}

public class AdminAiAssistantRequest
{
    /// <summary>
    /// Từ ngày (UTC) dùng để phân tích đơn hàng, nếu null sẽ mặc định 30 ngày trước.
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Đến ngày (UTC), nếu null sẽ mặc định thời điểm hiện tại.
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Ngôn ngữ mong muốn cho câu trả lời (vd: "vi" hoặc "en"). Mặc định: "vi".
    /// </summary>
    public string Language { get; set; } = "vi";
}

public class AdminAiAssistantResponse
{
    /// <summary>
    /// Tóm tắt chung về tình hình bán hàng.
    /// </summary>
    public string Overview { get; set; } = string.Empty;

    /// <summary>
    /// Gợi ý các danh mục nên ưu tiên nhập thêm.
    /// </summary>
    public List<string> RecommendedCategories { get; set; } = new();

    /// <summary>
    /// Gợi ý cụ thể tên sách nên nhập mới hoặc tăng tồn kho.
    /// </summary>
    public List<AdminAiBookSuggestion> BookSuggestions { get; set; } = new();

    /// <summary>
    /// Tóm tắt các điểm mạnh / yếu trong đánh giá của khách hàng.
    /// </summary>
    public string CustomerFeedbackSummary { get; set; } = string.Empty;
}

public class AdminAiBookSuggestion
{
    /// <summary>
    /// ISBN nếu sách đã tồn tại trong hệ thống (có thể rỗng nếu AI gợi ý nhập sách mới theo tên).
    /// </summary>
    public string? Isbn { get; set; }

    /// <summary>
    /// Tên sách gợi ý.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Danh mục / thể loại liên quan.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Lý do gợi ý (ví dụ: đang bán chạy, được đánh giá cao, thiếu hàng,...).
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}


