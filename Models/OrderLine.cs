using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("order_line")]
public class OrderLine
{
    [Key]
    [Column("order_line_id")]
    public long OrderLineId { get; set; }

    [Required]
    [Column("order_id")]
    public long OrderId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("isbn")]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [Column("qty")]
    public int Qty { get; set; }

    [Required]
    [Column("unit_price", TypeName = "decimal(12,2)")]
    public decimal UnitPrice { get; set; }

    // Navigation properties
    [ForeignKey("OrderId")]
    public virtual Order Order { get; set; } = null!;

    [ForeignKey("Isbn")]
    public virtual Book Book { get; set; } = null!;

    public virtual ICollection<ReturnLine> ReturnLines { get; set; } = new List<ReturnLine>();

    // Computed property
    [NotMapped]
    public decimal LineTotal => Qty * UnitPrice;
}
