using System.ComponentModel.DataAnnotations;
using BookStore.Api.Models;

namespace BookStore.Api.DTOs;

public class BookDto
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public decimal UnitPrice { get; set; }
    public int PublishYear { get; set; }
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public long PublisherId { get; set; }
    public string PublisherName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AuthorDto> Authors { get; set; } = new List<AuthorDto>();
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

    [Required(ErrorMessage = "Giá bán là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải lớn hơn hoặc bằng 0")]
    public decimal UnitPrice { get; set; }

    [Required(ErrorMessage = "Năm xuất bản là bắt buộc")]
    [Range(1900, 2100, ErrorMessage = "Năm xuất bản phải từ 1900 đến 2100")]
    public int PublishYear { get; set; }

    [Required(ErrorMessage = "Danh mục là bắt buộc")]
    public long CategoryId { get; set; }

    [Required(ErrorMessage = "Nhà xuất bản là bắt buộc")]
    public long PublisherId { get; set; }

    [MaxLength(500, ErrorMessage = "URL ảnh không được vượt quá 500 ký tự")]
    public string? ImageUrl { get; set; }

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

    [Required(ErrorMessage = "Giá bán là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải lớn hơn hoặc bằng 0")]
    public decimal UnitPrice { get; set; }

    [Required(ErrorMessage = "Năm xuất bản là bắt buộc")]
    [Range(1900, 2100, ErrorMessage = "Năm xuất bản phải từ 1900 đến 2100")]
    public int PublishYear { get; set; }

    [Required(ErrorMessage = "Danh mục là bắt buộc")]
    public long CategoryId { get; set; }

    [Required(ErrorMessage = "Nhà xuất bản là bắt buộc")]
    public long PublisherId { get; set; }

    [MaxLength(500, ErrorMessage = "URL ảnh không được vượt quá 500 ký tự")]
    public string? ImageUrl { get; set; }

    public List<long> AuthorIds { get; set; } = new List<long>();
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
}
