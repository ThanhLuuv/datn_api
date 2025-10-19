using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class PromotionDto
{
    public long PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DiscountPct { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public long IssuedBy { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Danh sách sách áp dụng khuyến mãi
    public List<PromotionBookDto> Books { get; set; } = new List<PromotionBookDto>();
    
    // Trạng thái khuyến mãi
    public string Status => GetPromotionStatus();
    
    private string GetPromotionStatus()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today < StartDate) return "Chưa bắt đầu";
        if (today > EndDate) return "Đã kết thúc";
        return "Đang diễn ra";
    }
}

public class PromotionBookDto
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string PublisherName { get; set; } = string.Empty;
}

public class CreatePromotionDto
{
    [Required(ErrorMessage = "Tên khuyến mãi là bắt buộc")]
    [MaxLength(200, ErrorMessage = "Tên khuyến mãi không được vượt quá 200 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Phần trăm giảm giá là bắt buộc")]
    [Range(0.01, 99.99, ErrorMessage = "Phần trăm giảm giá phải từ 0.01% đến 99.99%")]
    public decimal DiscountPct { get; set; }

    [Required(ErrorMessage = "Ngày bắt đầu là bắt buộc")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "Ngày kết thúc là bắt buộc")]
    public DateOnly EndDate { get; set; }

    [Required(ErrorMessage = "Danh sách sách áp dụng là bắt buộc")]
    [MinLength(1, ErrorMessage = "Phải chọn ít nhất 1 cuốn sách")]
    public List<string> BookIsbns { get; set; } = new List<string>();

    // Validation method
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "Ngày kết thúc không được trước ngày bắt đầu",
                new[] { nameof(EndDate) });
        }

        if (StartDate < DateOnly.FromDateTime(DateTime.Today))
        {
            yield return new ValidationResult(
                "Ngày bắt đầu không được trong quá khứ",
                new[] { nameof(StartDate) });
        }
    }
}

public class UpdatePromotionDto
{
    [Required(ErrorMessage = "Tên khuyến mãi là bắt buộc")]
    [MaxLength(200, ErrorMessage = "Tên khuyến mãi không được vượt quá 200 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Phần trăm giảm giá là bắt buộc")]
    [Range(0.01, 99.99, ErrorMessage = "Phần trăm giảm giá phải từ 0.01% đến 99.99%")]
    public decimal DiscountPct { get; set; }

    [Required(ErrorMessage = "Ngày bắt đầu là bắt buộc")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "Ngày kết thúc là bắt buộc")]
    public DateOnly EndDate { get; set; }

    [Required(ErrorMessage = "Danh sách sách áp dụng là bắt buộc")]
    [MinLength(1, ErrorMessage = "Phải chọn ít nhất 1 cuốn sách")]
    public List<string> BookIsbns { get; set; } = new List<string>();

    // Validation method
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(
                "Ngày kết thúc không được trước ngày bắt đầu",
                new[] { nameof(EndDate) });
        }
    }
}

public class PromotionSearchRequest
{
    public string? Name { get; set; }
    public decimal? MinDiscountPct { get; set; }
    public decimal? MaxDiscountPct { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Status { get; set; } // "active", "upcoming", "expired", "all"
    public long? IssuedBy { get; set; }
    public string? BookIsbn { get; set; } // Tìm khuyến mãi theo sách
    
    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    
    // Sorting
    public string SortBy { get; set; } = "CreatedAt";
    public string SortOrder { get; set; } = "desc"; // "asc" or "desc"
}

public class PromotionListResponse
{
    public List<PromotionDto> Promotions { get; set; } = new List<PromotionDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class PromotionStatsDto
{
    public int TotalPromotions { get; set; }
    public int ActivePromotions { get; set; }
    public int UpcomingPromotions { get; set; }
    public int ExpiredPromotions { get; set; }
    public decimal AverageDiscountPct { get; set; }
    public int TotalBooksInPromotion { get; set; }
}
