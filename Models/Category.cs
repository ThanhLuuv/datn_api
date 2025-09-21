using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("category")]
public class Category
{
    [Key]
    [Column("category_id")]
    public long CategoryId { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    // Navigation properties
    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
}
