using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class OrderDto
{
    public long OrderId { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime PlacedAt { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public DateTime? DeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public long? ApprovedBy { get; set; }
    public string? ApprovedByName { get; set; }
    public long? DeliveredBy { get; set; }
    public string? DeliveredByName { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
    public OrderInvoiceDto? Invoice { get; set; }
    public string? PaymentUrl { get; set; }
}

public class OrderLineDto
{
    public long OrderLineId { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public BookSummaryDto? Book { get; set; }
}

public class OrderInvoiceDto
{
    public long InvoiceId { get; set; }
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

public class OrderListResponse
{
    public List<OrderDto> Orders { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class SuggestedEmployeeDto
{
    public long EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AreaName { get; set; }
    public bool IsAreaMatched { get; set; }
    public int ActiveAssignedOrders { get; set; }
    public int TotalDeliveredOrders { get; set; }
}

public class OrderSearchRequest
{
    public string? Keyword { get; set; }
    public long? CustomerId { get; set; }
    public string? Status { get; set; } // Pending, Assigned, Delivered
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class BookSummaryDto
{
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public decimal AveragePrice { get; set; }
    public int PublishYear { get; set; }
    public long CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public long PublisherId { get; set; }
    public string? PublisherName { get; set; }
    public string? ImageUrl { get; set; }
    public int Stock { get; set; }
    public bool Status { get; set; }
}

public class ApproveOrderRequest
{
    [Required]
    public bool Approved { get; set; }
    public string? Note { get; set; }
}

public class AssignDeliveryRequest
{
    [Required]
    public long DeliveryEmployeeId { get; set; }
    public DateTime? DeliveryDate { get; set; }
}

public class ConfirmDeliveredRequest
{
    [Required]
    public bool Success { get; set; }
    public string? Note { get; set; }
}

public class CancelOrderRequest
{
    [Required(ErrorMessage = "Cancellation reason is required")]
    [MaxLength(500, ErrorMessage = "Cancellation reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;
    
    public string? Note { get; set; }
}

public class CreateOrderDto
{
    [Required(ErrorMessage = "Receiver name is required")]
    [MaxLength(150, ErrorMessage = "Receiver name cannot exceed 150 characters")]
    public string ReceiverName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Receiver phone is required")]
    [MaxLength(30, ErrorMessage = "Receiver phone cannot exceed 30 characters")]
    public string ReceiverPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Shipping address is required")]
    [MaxLength(300, ErrorMessage = "Shipping address cannot exceed 300 characters")]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Order lines are required")]
    [MinLength(1, ErrorMessage = "At least one order line is required")]
    public List<CreateOrderLineDto> Lines { get; set; } = new();
}

public class CreateOrderLineDto
{
    [Required(ErrorMessage = "ISBN is required")]
    [MaxLength(20, ErrorMessage = "ISBN cannot exceed 20 characters")]
    public string Isbn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Quantity is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public int Qty { get; set; }
}


