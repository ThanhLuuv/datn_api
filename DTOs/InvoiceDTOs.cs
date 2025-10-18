using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class InvoiceDto
{
    public long InvoiceId { get; set; }
    public long OrderId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InvoiceWithOrderDto
{
    public long InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long OrderId { get; set; }
    public DateTime PlacedAt { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
}

public class InvoiceWithOrderListResponse
{
    public List<InvoiceWithOrderDto> Invoices { get; set; } = new List<InvoiceWithOrderDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class InvoiceListResponse
{
    public List<InvoiceDto> Invoices { get; set; } = new List<InvoiceDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class InvoiceSearchRequest
{
    public long? OrderId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? PaymentStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
