using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("expense_voucher")]
public class ExpenseVoucher
{
    [Key]
    [Column("expense_voucher_id")]
    public long ExpenseVoucherId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("voucher_number")]
    public string VoucherNumber { get; set; } = string.Empty;

    [Required]
    [Column("voucher_date")]
    public DateTime VoucherDate { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("total_amount", TypeName = "decimal(12,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED

    [MaxLength(20)]
    [Column("expense_type")]
    public string ExpenseType { get; set; } = "RETURN_REFUND"; // RETURN_REFUND, OPERATIONAL, SUPPLIER_PAYMENT, SALARY, MARKETING, OTHER

    [Column("created_by")]
    public long? CreatedBy { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("CreatedBy")]
    public virtual Employee? Creator { get; set; }

    [ForeignKey("ApprovedBy")]
    public virtual Employee? Approver { get; set; }

    public virtual ICollection<ExpenseVoucherLine> ExpenseVoucherLines { get; set; } = new List<ExpenseVoucherLine>();
}

[Table("expense_voucher_line")]
public class ExpenseVoucherLine
{
    [Key]
    [Column("expense_voucher_line_id")]
    public long ExpenseVoucherLineId { get; set; }

    [Required]
    [Column("expense_voucher_id")]
    public long ExpenseVoucherId { get; set; }

    [MaxLength(200)]
    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("amount", TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    [Column("reference")]
    public string? Reference { get; set; } // Reference to return_id, order_id, etc.

    [MaxLength(20)]
    [Column("reference_type")]
    public string? ReferenceType { get; set; } // RETURN, ORDER, SUPPLIER, etc.

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey("ExpenseVoucherId")]
    public virtual ExpenseVoucher ExpenseVoucher { get; set; } = null!;
}
