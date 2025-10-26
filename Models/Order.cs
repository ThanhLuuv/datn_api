using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("order")]
public class Order
{
    [Key]
    [Column("order_id")]
    public long OrderId { get; set; }

    [Required]
    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Required]
    [Column("placed_at")]
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(150)]
    [Column("receiver_name")]
    public string ReceiverName { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [Column("receiver_phone")]
    public string ReceiverPhone { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    [Column("shipping_address")]
    public string ShippingAddress { get; set; } = string.Empty;

    [Column("delivery_date", TypeName = "date")]
    public DateTime? DeliveryDate { get; set; }

    [Required]
    [Column("status")]
    public OrderStatus Status { get; set; } = OrderStatus.PendingConfirmation;

    [MaxLength(255)]
    [Column("note")]
    public string? Note { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("delivered_by")]
    public long? DeliveredBy { get; set; }

    // Navigation properties
    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("ApprovedBy")]
    public virtual Employee? ApprovedByEmployee { get; set; }

    [ForeignKey("DeliveredBy")]
    public virtual Employee? DeliveredByEmployee { get; set; }

    public virtual ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    // Computed properties
    [NotMapped]
    public decimal TotalAmount => OrderLines?.Sum(line => line.Qty * line.UnitPrice) ?? 0;

    [NotMapped]
    public int TotalQuantity => OrderLines?.Sum(line => line.Qty) ?? 0;
}

public enum OrderStatus
{
    PendingConfirmation = 0,  // 0 - Chờ xác nhận
    Confirmed = 1,             // 1 - Đã xác nhận/Phân công
    Delivered = 2,             // 2 - Đã giao
    Cancelled = 3              // 3 - Đã hủy
}
