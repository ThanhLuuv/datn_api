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

public class AdminAiChatMessage
{
    /// <summary>
    /// Role trong cuộc hội thoại (user hoặc assistant).
    /// </summary>
    [Required]
    [RegularExpression("^(user|assistant|system)$", ErrorMessage = "Role must be user, assistant, or system")]
    public string Role { get; set; } = "user";

    /// <summary>
    /// Nội dung tin nhắn dạng text.
    /// </summary>
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;
}

public class AdminAiChatRequest
{
    /// <summary>
    /// Danh sách tin nhắn (giữ lịch sử hội thoại).
    /// </summary>
    [Required]
    public List<AdminAiChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Khoảng thời gian mong muốn lấy dữ liệu báo cáo. Mặc định: 30 ngày gần nhất.
    /// </summary>
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Ngôn ngữ trả lời (vi/en). Mặc định: vi.
    /// </summary>
    [MaxLength(8)]
    public string Language { get; set; } = "vi";

    /// <summary>
    /// Có kèm snapshot tồn kho hay không (mặc định: có để AI trả lời chính xác các câu hỏi inventory).
    /// </summary>
    public bool IncludeInventorySnapshot { get; set; } = true;

    /// <summary>
    /// Có kèm tỷ trọng danh mục hay không.
    /// </summary>
    public bool IncludeCategoryShare { get; set; } = true;
}

public class AdminAiChatResponse
{
    /// <summary>
    /// Lịch sử hội thoại mới nhất (bao gồm tin nhắn vừa gửi).
    /// </summary>
    public List<AdminAiChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Phản hồi plain text từ AI (để hiển thị nhanh).
    /// </summary>
    public string? PlainTextAnswer { get; set; }

    /// <summary>
    /// Phản hồi định dạng Markdown (nếu có).
    /// </summary>
    public string? MarkdownAnswer { get; set; }

    /// <summary>
    /// Danh sách nguồn dữ liệu nội bộ đã được sử dụng để trả lời (vd: profit_report, inventory_snapshot).
    /// </summary>
    public List<string> DataSources { get; set; } = new();

    /// <summary>
    /// Payload có cấu trúc (tuỳ chọn) khi AI trả về bảng biểu hoặc đề xuất chi tiết.
    /// </summary>
    public Dictionary<string, object>? Insights { get; set; }
}


