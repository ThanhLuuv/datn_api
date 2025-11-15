using System.ComponentModel.DataAnnotations;
using BookStore.Api.Models;

namespace BookStore.Api.DTOs;

public class CustomerProfileDto
{
    public long CustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = "Other";
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateCustomerProfileDto
{
    [Required(ErrorMessage = "Họ là bắt buộc")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên là bắt buộc")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giới tính là bắt buộc")]
    [RegularExpression("^(Male|Female|Other)$")]
    public string Gender { get; set; } = "Other";

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    // Cho phép cập nhật email liên hệ của Customer (không ảnh hưởng đến Account.Email dùng để đăng nhập)
    [MaxLength(191)]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string? Email { get; set; }
}



