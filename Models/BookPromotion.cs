using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("book_promotion")]
public class BookPromotion
{
    [Required]
    [MaxLength(20)]
    [Column("isbn")]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [Column("promotion_id")]
    public long PromotionId { get; set; }

    // Navigation properties
    [ForeignKey("Isbn")]
    public virtual Book Book { get; set; } = null!;

    [ForeignKey("PromotionId")]
    public virtual Promotion Promotion { get; set; } = null!;
}
