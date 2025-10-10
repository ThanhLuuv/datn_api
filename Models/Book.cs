using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("book")]
public class Book
{
    [Key]
    [MaxLength(20)]
    [Column("isbn")]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("page_count")]
    public int PageCount { get; set; }

    [Required]
    [Column("average_price", TypeName = "decimal(12,2)")]
    public decimal AveragePrice { get; set; }

    [Required]
    [Column("publish_year")]
    public int PublishYear { get; set; }

    [Required]
    [Column("category_id")]
    public long CategoryId { get; set; }

    [Required]
    [Column("publisher_id")]
    public long PublisherId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("stock")]
    public int Stock { get; set; } = 0;

    [Column("status")]
    public bool Status { get; set; } = true;

    // Navigation properties
    [ForeignKey("CategoryId")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("PublisherId")]
    public virtual Publisher Publisher { get; set; } = null!;

    public virtual ICollection<AuthorBook> AuthorBooks { get; set; } = new List<AuthorBook>();
    public virtual ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public virtual ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();
    public virtual ICollection<BookPromotion> BookPromotions { get; set; } = new List<BookPromotion>();
    public virtual ICollection<PriceChange> PriceChanges { get; set; } = new List<PriceChange>();
}
