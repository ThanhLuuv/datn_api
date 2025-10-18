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

    [Required]
    [Column("status")]
    public ReturnStatus Status { get; set; } = ReturnStatus.Pending;

    [Column("processed_by")]
    public long? ProcessedBy { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [MaxLength(500)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("apply_deduction")]
    public bool ApplyDeduction { get; set; } = false;

    [Column("deduction_percent", TypeName = "decimal(5,2)")]
    public decimal DeductionPercent { get; set; } = 0;

    [Column("deduction_amount", TypeName = "decimal(12,2)")]
    public decimal DeductionAmount { get; set; } = 0;

    [Column("final_amount", TypeName = "decimal(12,2)")]
    public decimal FinalAmount { get; set; } = 0;

    // Navigation properties
    [ForeignKey("InvoiceId")]
    public virtual Invoice Invoice { get; set; } = null!;

    [ForeignKey("ProcessedBy")]
    public virtual Employee? ProcessedByEmployee { get; set; }

    public virtual ICollection<ReturnLine> ReturnLines { get; set; } = new List<ReturnLine>();

    // Computed property
    [NotMapped]
    public decimal TotalAmount => ReturnLines?.Sum(line => line.Amount) ?? 0;
}

public enum ReturnStatus
{
    Pending,    // 0 - Chờ xử lý
    Approved,    // 1 - Đã duyệt
    Rejected,    // 2 - Từ chối
    Processed    // 3 - Đã xử lý
}
