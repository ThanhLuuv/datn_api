using System.ComponentModel.DataAnnotations;
using BookStore.Api.Models;
using Microsoft.AspNetCore.Http;

namespace BookStore.Api.DTOs;

public class BookDto
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public int PublishYear { get; set; }
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public long PublisherId { get; set; }
    public string PublisherName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Stock { get; set; }
    public bool Status { get; set; }
    public bool HasPromotion { get; set; }
    public int? TotalSold { get; set; }
    public List<AuthorDto> Authors { get; set; } = new List<AuthorDto>();
    public List<BookPromotionDto> ActivePromotions { get; set; } = new List<BookPromotionDto>();

    /// <summary>
    /// Tóm tắt / đánh giá sản phẩm do AI sinh ra (tuỳ chọn, chỉ có ở các API AI).
    /// </summary>
    public string? AiSummary { get; set; }

    /// <summary>
    /// Lý do AI cho rằng cuốn sách này phù hợp với nhu cầu (tuỳ chọn).
    /// </summary>
    public string? AiReason { get; set; }
}


public class CreateBookDto
{
    [Required(ErrorMessage = "ISBN là bắt buộc")]
    [MaxLength(20, ErrorMessage = "ISBN không được vượt quá 20 ký tự")]
    public string Isbn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên sách là bắt buộc")]
    [MaxLength(300, ErrorMessage = "Tên sách không được vượt quá 300 ký tự")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số trang là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải lớn hơn 0")]
    public int PageCount { get; set; }

    [Required(ErrorMessage = "Initial price is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Initial price must be greater than or equal to 0")]
    public decimal InitialPrice { get; set; }

    [Required(ErrorMessage = "Năm xuất bản là bắt buộc")]
    [Range(1900, 2100, ErrorMessage = "Năm xuất bản phải từ 1900 đến 2100")]
    public int PublishYear { get; set; }

    [Required(ErrorMessage = "Danh mục là bắt buộc")]
    public long CategoryId { get; set; }

    [Required(ErrorMessage = "Nhà xuất bản là bắt buộc")]
    public long PublisherId { get; set; }

    public IFormFile? ImageFile { get; set; }

    public List<long> AuthorIds { get; set; } = new List<long>();
}

public class UpdateBookDto
{
    [Required(ErrorMessage = "Tên sách là bắt buộc")]
    [MaxLength(300, ErrorMessage = "Tên sách không được vượt quá 300 ký tự")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số trang là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải lớn hơn 0")]
    public int PageCount { get; set; }

    [Required(ErrorMessage = "Initial price is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Initial price must be greater than or equal to 0")]
    public decimal InitialPrice { get; set; }

    [Required(ErrorMessage = "Năm xuất bản là bắt buộc")]
    [Range(1900, 2100, ErrorMessage = "Năm xuất bản phải từ 1900 đến 2100")]
    public int PublishYear { get; set; }

    [Required(ErrorMessage = "Danh mục là bắt buộc")]
    public long CategoryId { get; set; }

    [Required(ErrorMessage = "Nhà xuất bản là bắt buộc")]
    public long PublisherId { get; set; }

    [MaxLength(500, ErrorMessage = "URL ảnh không được vượt quá 500 ký tự")]
    public string? ImageUrl { get; set; }

    public IFormFile? ImageFile { get; set; }

    public List<long> AuthorIds { get; set; } = new List<long>();

    [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho phải lớn hơn hoặc bằng 0")]
    public int? Stock { get; set; }

    public bool? Status { get; set; }
}

public class BookListResponse
{
    public List<BookDto> Books { get; set; } = new List<BookDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class BookSearchRequest
{
    public string? SearchTerm { get; set; }
    public long? CategoryId { get; set; }
    public long? PublisherId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinYear { get; set; }
    public int? MaxYear { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; } = "Title";
    public string? SortDirection { get; set; } = "asc";
    public bool IncludeOutOfStock { get; set; } = false;
    public bool IncludeInactive { get; set; } = false;
}

public class PriceChangeDto
{
    public long PriceChangeId { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ChangedAt { get; set; }
    public long EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool? IsActive { get; set; }
}

public class CreatePriceChangeDto
{
    [Required(ErrorMessage = "ISBN is required")]
    [MaxLength(20, ErrorMessage = "ISBN cannot exceed 20 characters")]
    public string Isbn { get; set; } = string.Empty;

    [Required(ErrorMessage = "New price is required")]
    [Range(0, double.MaxValue, ErrorMessage = "New price must be greater than or equal to 0")]
    public decimal NewPrice { get; set; }

    [Required(ErrorMessage = "Effective date is required")]
    public DateTime EffectiveDate { get; set; }

    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }
}

public class PriceChangeListResponse
{
    public List<PriceChangeDto> PriceChanges { get; set; } = new List<PriceChangeDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class PriceChangeSearchRequest
{
    public string? Isbn { get; set; }
    public long? EmployeeId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? IsActive { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class BookPromotionDto
{
    public long PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DiscountPct { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
