using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("invoice")]
public class Invoice
{
    [Key]
    [Column("invoice_id")]
    public long InvoiceId { get; set; }

    [Required]
    [Column("order_id")]
    public long OrderId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("total_amount", TypeName = "decimal(14,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    [Column("tax_amount", TypeName = "decimal(14,2)")]
    public decimal TaxAmount { get; set; }

    // Navigation properties
    [ForeignKey("OrderId")]
    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<Return> Returns { get; set; } = new List<Return>();

    // Computed property
    [NotMapped]
    public decimal SubTotal => TotalAmount - TaxAmount;
}
