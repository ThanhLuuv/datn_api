using System.ComponentModel.DataAnnotations;
using BookStore.Api.Models;

namespace BookStore.Api.DTOs;

public class CreateReturnLineDto
{
    [Required]
    [MaxLength(20)]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int QtyReturned { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}

public class CreateReturnDto
{
    [Required]
    public long InvoiceId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateReturnLineDto> Lines { get; set; } = new();

    public bool ApplyDeduction { get; set; } = false;

    [Range(0, 100)]
    public decimal DeductionPercent { get; set; } = 0;

    public bool CreatePayout { get; set; } = false;
}

public class ReturnLineDto
{
    public long ReturnLineId { get; set; }
    public long OrderLineId { get; set; }
    public int QtyReturned { get; set; }
    public decimal Amount { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
}

public class ReturnDto
{
    public long ReturnId { get; set; }
    public long InvoiceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Reason { get; set; }
    public ReturnStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public long? ProcessedBy { get; set; }
    public string? ProcessedByEmployeeName { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public bool ApplyDeduction { get; set; }
    public decimal DeductionPercent { get; set; }
    public decimal DeductionAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public List<ReturnLineDto> Lines { get; set; } = new();
    public ReturnOrderDto? Order { get; set; }
}

public class ReturnSearchRequest
{
    public long? InvoiceId { get; set; }
    public ReturnStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class UpdateReturnStatusRequest
{
    [Required]
    public ReturnStatus Status { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class ReturnListResponse
{
    public List<ReturnDto> Returns { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ReturnOrderDto
{
    public long OrderId { get; set; }
    public DateTime PlacedAt { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
}


