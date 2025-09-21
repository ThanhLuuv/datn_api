using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class PurchaseOrderDto
{
    public long PoId { get; set; }
    public long PublisherId { get; set; }
    public string PublisherName { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public long CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
    public List<PurchaseOrderLineDto> Lines { get; set; } = new List<PurchaseOrderLineDto>();
}

public class PurchaseOrderLineDto
{
    public long PoLineId { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public int QtyOrdered { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreatePurchaseOrderDto
{
    [Required(ErrorMessage = "Nhà xuất bản là bắt buộc")]
    public long PublisherId { get; set; }

    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Note { get; set; }

    [Required(ErrorMessage = "Danh sách sách đặt mua là bắt buộc")]
    [MinLength(1, ErrorMessage = "Phải có ít nhất 1 sách trong đơn đặt mua")]
    public List<CreatePurchaseOrderLineDto> Lines { get; set; } = new List<CreatePurchaseOrderLineDto>();
}

public class CreatePurchaseOrderLineDto
{
    [Required(ErrorMessage = "ISBN là bắt buộc")]
    [MaxLength(20, ErrorMessage = "ISBN không được vượt quá 20 ký tự")]
    public string Isbn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số lượng đặt mua là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng đặt mua phải lớn hơn 0")]
    public int QtyOrdered { get; set; }

    [Required(ErrorMessage = "Đơn giá là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Đơn giá phải lớn hơn hoặc bằng 0")]
    public decimal UnitPrice { get; set; }
}

public class UpdatePurchaseOrderDto
{
    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Note { get; set; }

    [Required(ErrorMessage = "Danh sách sách đặt mua là bắt buộc")]
    [MinLength(1, ErrorMessage = "Phải có ít nhất 1 sách trong đơn đặt mua")]
    public List<UpdatePurchaseOrderLineDto> Lines { get; set; } = new List<UpdatePurchaseOrderLineDto>();
}

public class UpdatePurchaseOrderLineDto
{
    public long? PoLineId { get; set; } // null for new lines

    [Required(ErrorMessage = "ISBN là bắt buộc")]
    [MaxLength(20, ErrorMessage = "ISBN không được vượt quá 20 ký tự")]
    public string Isbn { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số lượng đặt mua là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng đặt mua phải lớn hơn 0")]
    public int QtyOrdered { get; set; }

    [Required(ErrorMessage = "Đơn giá là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Đơn giá phải lớn hơn hoặc bằng 0")]
    public decimal UnitPrice { get; set; }
}

public class PurchaseOrderListResponse
{
    public List<PurchaseOrderDto> PurchaseOrders { get; set; } = new List<PurchaseOrderDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class PurchaseOrderSearchRequest
{
    public long? PublisherId { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; } = "OrderedAt";
    public string? SortDirection { get; set; } = "desc";
}
