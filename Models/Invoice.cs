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
    [Column("invoice_number")]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    [Column("tax_amount", TypeName = "decimal(12,2)")]
    public decimal TaxAmount { get; set; }

    [Required]
    [Column("payment_status")]
    [MaxLength(20)]
    public string PaymentStatus { get; set; } = "PENDING"; // PENDING, PAID, FAILED, REFUNDED

    [Column("payment_method")]
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [Column("payment_reference")]
    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("OrderId")]
    public virtual Order Order { get; set; } = null!;
    public virtual ICollection<Return> Returns { get; set; } = new List<Return>();
}

public enum PaymentStatus
{
    PENDING,
    PAID,
    FAILED,
    REFUNDED
}