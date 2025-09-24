using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("rating")]
public class Rating
{
    [Key]
    [Column("rating_id")]
    public long RatingId { get; set; }

    [Required]
    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("isbn")]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [Range(1, 5)]
    [Column("stars")]
    public int Stars { get; set; }

    [MaxLength(1000)]
    [Column("comment")]
    public string? Comment { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public Book? Book { get; set; }
}


