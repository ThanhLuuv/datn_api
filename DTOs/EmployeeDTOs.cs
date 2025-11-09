using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class EmployeeDto
{
    public long EmployeeId { get; set; }
    public long AccountId { get; set; }
    public long DepartmentId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string AccountEmail { get; set; } = string.Empty;
    public long RoleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateEmployeeWithAccountRequest
{
    // Account fields
    [Required]
    [EmailAddress]
    [MaxLength(191)]
    public string AccountEmail { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public long RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    // Employee fields
    [Required]
    public long DepartmentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Male|Female|Other)$")]
    public string Gender { get; set; } = "Other";

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(191)]
    [EmailAddress]
    public string? EmployeeEmail { get; set; }
}

public class UpdateEmployeeDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Male|Female|Other)$")]
    public string Gender { get; set; } = "Other";

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(191)]
    [EmailAddress]
    public string? EmployeeEmail { get; set; }

    [Required]
    public long DepartmentId { get; set; }
}

public class EmployeeListResponse
{
    public List<EmployeeDto> Employees { get; set; } = new List<EmployeeDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}



