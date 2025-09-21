using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("return_line")]
public class ReturnLine
{
    [Key]
    [Column("return_line_id")]
    public long ReturnLineId { get; set; }

    [Required]
    [Column("return_id")]
    public long ReturnId { get; set; }

    [Required]
    [Column("order_line_id")]
    public long OrderLineId { get; set; }

    [Required]
    [Column("qty_returned")]
    public int QtyReturned { get; set; }

    [Required]
    [Column("amount", TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    // Navigation properties
    [ForeignKey("ReturnId")]
    public virtual Return Return { get; set; } = null!;

    [ForeignKey("OrderLineId")]
    public virtual OrderLine OrderLine { get; set; } = null!;
}
