using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("return")]
public class Return
{
    [Key]
    [Column("return_id")]
    public long ReturnId { get; set; }

    [Required]
    [Column("invoice_id")]
    public long InvoiceId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    // Navigation properties
    [ForeignKey("InvoiceId")]
    public virtual Invoice Invoice { get; set; } = null!;

    public virtual ICollection<ReturnLine> ReturnLines { get; set; } = new List<ReturnLine>();

    // Computed property
    [NotMapped]
    public decimal TotalAmount => ReturnLines?.Sum(line => line.Amount) ?? 0;
}
