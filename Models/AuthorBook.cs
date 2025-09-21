using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("author_book")]
public class AuthorBook
{
    [Key]
    [Column("author_id")]
    public long AuthorId { get; set; }

    [Key]
    [MaxLength(20)]
    [Column("isbn")]
    public string Isbn { get; set; } = string.Empty;

    // Navigation properties
    [ForeignKey("AuthorId")]
    public virtual Author Author { get; set; } = null!;

    [ForeignKey("Isbn")]
    public virtual Book Book { get; set; } = null!;
}
