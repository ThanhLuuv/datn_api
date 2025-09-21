using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("goods_receipt")]
public class GoodsReceipt
{
    [Key]
    [Column("gr_id")]
    public long GrId { get; set; }

    [Required]
    [Column("po_id")]
    public long PoId { get; set; }

    [Required]
    [Column("received_at")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("created_by")]
    public long CreatedBy { get; set; }

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    // Navigation properties
    [ForeignKey("PoId")]
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;

    [ForeignKey("CreatedBy")]
    public virtual Employee CreatedByEmployee { get; set; } = null!;

    public virtual ICollection<GoodsReceiptLine> GoodsReceiptLines { get; set; } = new List<GoodsReceiptLine>();

    // Computed properties
    [NotMapped]
    public decimal TotalAmount => GoodsReceiptLines?.Sum(line => line.QtyReceived * line.UnitCost) ?? 0;

    [NotMapped]
    public int TotalQuantity => GoodsReceiptLines?.Sum(line => line.QtyReceived) ?? 0;
}
