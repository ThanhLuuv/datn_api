using System.ComponentModel.DataAnnotations;
using BookStore.Api.Models;

namespace BookStore.Api.DTOs;

public class AuthorDto
{
    public long AuthorId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public int BookCount { get; set; }
}

public class CreateAuthorDto
{
    [Required(ErrorMessage = "Tên là bắt buộc")]
    [MaxLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ là bắt buộc")]
    [MaxLength(100, ErrorMessage = "Họ không được vượt quá 100 ký tự")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giới tính là bắt buộc")]
    public Gender Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự")]
    public string? Address { get; set; }

    [MaxLength(191, ErrorMessage = "Email không được vượt quá 191 ký tự")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string? Email { get; set; }
}

public class UpdateAuthorDto
{
    [Required(ErrorMessage = "Tên là bắt buộc")]
    [MaxLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ là bắt buộc")]
    [MaxLength(100, ErrorMessage = "Họ không được vượt quá 100 ký tự")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giới tính là bắt buộc")]
    public Gender Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự")]
    public string? Address { get; set; }

    [MaxLength(191, ErrorMessage = "Email không được vượt quá 191 ký tự")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string? Email { get; set; }
}
