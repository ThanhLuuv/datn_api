using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class DepartmentDto
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int EmployeeCount { get; set; }
}

public class CreateDepartmentDto
{
    [Required(ErrorMessage = "Tên phòng ban là bắt buộc")]
    [MaxLength(150, ErrorMessage = "Tên phòng ban không được vượt quá 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    public string? Description { get; set; }
}

public class UpdateDepartmentDto
{
    [Required(ErrorMessage = "Tên phòng ban là bắt buộc")]
    [MaxLength(150, ErrorMessage = "Tên phòng ban không được vượt quá 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    public string? Description { get; set; }
}

public class DepartmentListResponse
{
    public List<DepartmentDto> Departments { get; set; } = new List<DepartmentDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
