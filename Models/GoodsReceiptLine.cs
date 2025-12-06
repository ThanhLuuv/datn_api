using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("goods_receipt_line")]
public class GoodsReceiptLine
{
    [Key]
    [Column("gr_line_id")]
    public long GrLineId { get; set; }

    [Required]
    [Column("gr_id")]
    public long GrId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("isbn")]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [Column("qty_received")]
    public int QtyReceived { get; set; }

    [Required]
    [Column("unit_cost", TypeName = "decimal(12,2)")]
    public decimal UnitCost { get; set; }

    // Navigation properties
    [ForeignKey("GrId")]
    public virtual GoodsReceipt GoodsReceipt { get; set; } = null!;

    [ForeignKey("Isbn")]
    public virtual Book? Book { get; set; }

    // Computed property
    [NotMapped]
    public decimal LineTotal => QtyReceived * UnitCost;
}
