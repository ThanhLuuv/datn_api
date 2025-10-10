using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("promotion")]
public class Promotion
{
    [Key]
    [Column("promotion_id")]
    public long PromotionId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("discount_pct", TypeName = "decimal(5,2)")]
    public decimal DiscountPct { get; set; }

    [Required]
    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Required]
    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    [Required]
    [Column("issued_by")]
    public long IssuedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("IssuedBy")]
    public virtual Employee IssuedByEmployee { get; set; } = null!;

    public virtual ICollection<BookPromotion> BookPromotions { get; set; } = new List<BookPromotion>();

    // Computed property for books
    [NotMapped]
    public virtual ICollection<Book> Books => BookPromotions.Select(bp => bp.Book).ToList();
}
