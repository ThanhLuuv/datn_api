using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("purchase_order_line")]
public class PurchaseOrderLine
{
    [Key]
    [Column("po_line_id")]
    public long PoLineId { get; set; }

    [Required]
    [Column("po_id")]
    public long PoId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("isbn")]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [Column("qty_ordered")]
    public int QtyOrdered { get; set; }

    [Required]
    [Column("unit_price", TypeName = "decimal(12,2)")]
    public decimal UnitPrice { get; set; }

    // Navigation properties
    [ForeignKey("PoId")]
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;

    [ForeignKey("Isbn")]
    public virtual Book Book { get; set; } = null!;

    // Computed property
    [NotMapped]
    public decimal LineTotal => QtyOrdered * UnitPrice;
}
