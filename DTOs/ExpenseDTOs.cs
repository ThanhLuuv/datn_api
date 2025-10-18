using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class CreateExpenseVoucherDto
{
    [Required]
    public DateTime VoucherDate { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(20)]
    public string ExpenseType { get; set; } = "RETURN_REFUND";

    [Required]
    public List<CreateExpenseVoucherLineDto> Lines { get; set; } = new();
}

public class CreateExpenseVoucherLineDto
{
    [MaxLength(200)]
    public string? Description { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    [MaxLength(20)]
    public string? ReferenceType { get; set; }
}

public class ExpenseVoucherDto
{
    public long ExpenseVoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; }
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ExpenseType { get; set; } = string.Empty;
    public long? CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public long? ApprovedBy { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ExpenseVoucherLineDto> Lines { get; set; } = new();
}

public class ExpenseVoucherLineDto
{
    public long ExpenseVoucherLineId { get; set; }
    public long ExpenseVoucherId { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? ReferenceType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExpenseVoucherResponse
{
    public List<ExpenseVoucherDto> ExpenseVouchers { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ApproveExpenseVoucherDto
{
    [Required]
    public long ExpenseVoucherId { get; set; }

    [MaxLength(500)]
    public string? ApprovalNote { get; set; }
}

public class RejectExpenseVoucherDto
{
    [Required]
    public long ExpenseVoucherId { get; set; }

    [Required]
    [MaxLength(500)]
    public string RejectionReason { get; set; } = string.Empty;
}
