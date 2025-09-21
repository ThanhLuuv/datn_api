using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class CategoryDto
{
    public long CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int BookCount { get; set; }
}

public class CreateCategoryDto
{
    [Required(ErrorMessage = "Tên danh mục là bắt buộc")]
    [MaxLength(150, ErrorMessage = "Tên danh mục không được vượt quá 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    public string? Description { get; set; }
}

public class UpdateCategoryDto
{
    [Required(ErrorMessage = "Tên danh mục là bắt buộc")]
    [MaxLength(150, ErrorMessage = "Tên danh mục không được vượt quá 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    public string? Description { get; set; }
}

public class CategoryListResponse
{
    public List<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
