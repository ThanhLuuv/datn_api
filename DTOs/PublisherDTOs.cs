using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class PublisherDto
{
    public long PublisherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int BookCount { get; set; }
}

public class CreatePublisherDto
{
    [Required(ErrorMessage = "Tên nhà xuất bản là bắt buộc")]
    [StringLength(150, ErrorMessage = "Tên nhà xuất bản không được vượt quá 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ là bắt buộc")]
    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(191, ErrorMessage = "Email không được vượt quá 191 ký tự")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự")]
    public string Phone { get; set; } = string.Empty;
}

public class UpdatePublisherDto
{
    [Required(ErrorMessage = "Tên nhà xuất bản là bắt buộc")]
    [StringLength(150, ErrorMessage = "Tên nhà xuất bản không được vượt quá 150 ký tự")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ là bắt buộc")]
    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(191, ErrorMessage = "Email không được vượt quá 191 ký tự")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự")]
    public string Phone { get; set; } = string.Empty;
}

public class PublisherListResponse
{
    public List<PublisherDto> Publishers { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}