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

    /// <summary>
    /// Giá thị trường tham khảo (từ các sàn TMĐT / nhà sách khác) và dùng để gợi ý giá bán.
    /// </summary>
    public string? MarketPrice { get; set; }

    /// <summary>
    /// Tên nguồn / sàn thương mại điện tử cung cấp giá.
    /// </summary>
    public string? MarketSourceName { get; set; }

    /// <summary>
    /// Đường dẫn nguồn giá (nếu có).
    /// </summary>
    public string? MarketSourceUrl { get; set; }

    /// <summary>
    /// ISBN đề xuất (đảm bảo duy nhất trong hệ thống).
    /// </summary>
    public string? SuggestedIsbn { get; set; }

    /// <summary>
    /// Mã danh mục gợi ý (matching DB category khi có).
    /// </summary>
    public string? SuggestedCategoryId { get; set; }

    /// <summary>
    /// Tên tác giả chính (AI tự động đề xuất).
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// Tên nhà xuất bản (AI đề xuất).
    /// </summary>
    public string? PublisherName { get; set; }

    /// <summary>
    /// Số trang tham khảo.
    /// </summary>
    public int? PageCount { get; set; }

    /// <summary>
    /// Giá bán đề xuất trong hệ thống (VNĐ).
    /// </summary>
    public decimal? SuggestedPrice { get; set; }

    /// <summary>
    /// Mô tả / tóm tắt nội dung sách.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Năm xuất bản gợi ý.
    /// </summary>
    public int? PublishYear { get; set; }

    /// <summary>
    /// Số lượng tồn kho muốn đặt ban đầu.
    /// </summary>
    public int? SuggestedStock { get; set; }
}

public class AdminAiImportBookRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Isbn { get; set; }

    public long? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    public long? PublisherId { get; set; }
    public string? PublisherName { get; set; }

    public string? AuthorName { get; set; }

    public int? PageCount { get; set; }
    public int? PublishYear { get; set; }

    public decimal? SuggestedPrice { get; set; }
    public int? Stock { get; set; }

    public string? Description { get; set; }
}

public class AdminAiImportBookResponse
{
    public BookDto Book { get; set; } = new();
}

public class AdminAiVoiceRequest
{
    [Required]
    public string AudioBase64 { get; set; } = string.Empty;

    public string? MimeType { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    [MaxLength(8)]
    public string Language { get; set; } = "vi";

    public bool IncludeInventorySnapshot { get; set; } = true;
    public bool IncludeCategoryShare { get; set; } = true;
}

public class AdminAiVoiceResponse
{
    public string? Transcript { get; set; }
    public string? AnswerText { get; set; }
    public string? AudioBase64 { get; set; }
    public string? AudioMimeType { get; set; }
    public List<string> DataSources { get; set; } = new();
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

public class AiSearchRequest
{
    [Required]
    [MaxLength(3000)]
    public string Query { get; set; } = string.Empty;

    [Range(1, 15)]
    public int TopK { get; set; } = 5;

    public List<string>? RefTypes { get; set; }

    [MaxLength(8)]
    public string Language { get; set; } = "vi";

    public bool IncludeDebugDocuments { get; set; } = true;
}

public class AiSearchDocumentDto
{
    public long Id { get; set; }
    public string RefType { get; set; } = string.Empty;
    public string RefId { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class AiSearchResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<AiSearchDocumentDto> Documents { get; set; } = new();
    public Dictionary<string, object?>? Metadata { get; set; }
}

public class AiSearchReindexRequest
{
    /// <summary>
    /// Danh sách ref_type muốn index lại. Nếu null -> tất cả.
    /// </summary>
    public List<string>? RefTypes { get; set; }

    /// <summary>
    /// Xoá dữ liệu cũ trước khi index.
    /// </summary>
    public bool TruncateBeforeInsert { get; set; } = true;

    /// <summary>
    /// Giới hạn số sách tối đa lấy để tránh prompt quá lớn.
    /// </summary>
    [Range(50, 2000)]
    public int MaxBooks { get; set; } = 800;

    /// <summary>
    /// Giới hạn số khách hàng.
    /// </summary>
    [Range(50, 2000)]
    public int MaxCustomers { get; set; } = 300;

    /// <summary>
    /// Giới hạn số đơn hàng.
    /// </summary>
    [Range(50, 2000)]
    public int MaxOrders { get; set; } = 400;

    /// <summary>
    /// Khoảng ngày lịch sử tính toán báo cáo tổng hợp.
    /// </summary>
    [Range(30, 365)]
    public int HistoryDays { get; set; } = 180;
}

public class AiSearchReindexResponse
{
    public int IndexedDocuments { get; set; }
    public List<string> RefTypes { get; set; } = new();
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}

public class TextToSqlRequest
{
    [Required]
    [MaxLength(1000)]
    public string Question { get; set; } = string.Empty;

    [Range(1, 200)]
    public int? MaxRows { get; set; }
}

public class TextToSqlResponse
{
    public string Question { get; set; } = string.Empty;
    public string? SqlQuery { get; set; }
    public string? Answer { get; set; }
    public int RowCount { get; set; }
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public string? DataPreview { get; set; }
}


