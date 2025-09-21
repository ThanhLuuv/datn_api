using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class GoodsReceiptDto
{
    public long GrId { get; set; }
    public long PoId { get; set; }
    public string PurchaseOrderInfo { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public long CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
    public List<GoodsReceiptLineDto> Lines { get; set; } = new List<GoodsReceiptLineDto>();
}

public class GoodsReceiptLineDto
{
    public long GrLineId { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public int QtyReceived { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreateGoodsReceiptDto
{
    [Required(ErrorMessage = "Mã đơn đặt mua là bắt buộc")]
    public long PoId { get; set; }

    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Note { get; set; }

    [Required(ErrorMessage = "Danh sách sách nhập là bắt buộc")]
    [MinLength(1, ErrorMessage = "Phải có ít nhất 1 sách trong phiếu nhập")]
    public List<CreateGoodsReceiptLineDto> Lines { get; set; } = new List<CreateGoodsReceiptLineDto>();
}

public class CreateGoodsReceiptLineDto
{
    [Required(ErrorMessage = "Số lượng nhập là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng nhập phải lớn hơn 0")]
    public int QtyReceived { get; set; }

    [Required(ErrorMessage = "Giá nhập là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá nhập phải lớn hơn hoặc bằng 0")]
    public decimal UnitCost { get; set; }
}

public class UpdateGoodsReceiptDto
{
    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
    public string? Note { get; set; }

    [Required(ErrorMessage = "Danh sách sách nhập là bắt buộc")]
    [MinLength(1, ErrorMessage = "Phải có ít nhất 1 sách trong phiếu nhập")]
    public List<UpdateGoodsReceiptLineDto> Lines { get; set; } = new List<UpdateGoodsReceiptLineDto>();
}

public class UpdateGoodsReceiptLineDto
{
    [Required(ErrorMessage = "Số lượng nhập là bắt buộc")]
    [Range(1, int.MaxValue, ErrorMessage = "Số lượng nhập phải lớn hơn 0")]
    public int QtyReceived { get; set; }

    [Required(ErrorMessage = "Giá nhập là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá nhập phải lớn hơn hoặc bằng 0")]
    public decimal UnitCost { get; set; }
}

public class GoodsReceiptListResponse
{
    public List<GoodsReceiptDto> GoodsReceipts { get; set; } = new List<GoodsReceiptDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class GoodsReceiptSearchRequest
{
    public long? PoId { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; } = "ReceivedAt";
    public string? SortDirection { get; set; } = "desc";
}
