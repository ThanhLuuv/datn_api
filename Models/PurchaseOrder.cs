using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("purchase_order")]
public class PurchaseOrder
{
    [Key]
    [Column("po_id")]
    public long PoId { get; set; }

    [Required]
    [Column("publisher_id")]
    public long PublisherId { get; set; }

    [Required]
    [Column("ordered_at")]
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("created_by")]
    public long CreatedBy { get; set; }

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [Column("status_id")]
    public long? StatusId { get; set; }

    [MaxLength(500)]
    [Column("order_file_url")]
    public string? OrderFileUrl { get; set; }

    // Navigation properties
    [ForeignKey("PublisherId")]
    public virtual Publisher Publisher { get; set; } = null!;

    [ForeignKey("CreatedBy")]
    public virtual Employee CreatedByEmployee { get; set; } = null!;

    [ForeignKey("StatusId")]
    public virtual PurchaseOrderStatus? Status { get; set; }

    public virtual ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();
    public virtual ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();

    // Computed properties
    [NotMapped]
    public decimal TotalAmount => PurchaseOrderLines?.Sum(line => line.QtyOrdered * line.UnitPrice) ?? 0;

    [NotMapped]
    public int TotalQuantity => PurchaseOrderLines?.Sum(line => line.QtyOrdered) ?? 0;
}
