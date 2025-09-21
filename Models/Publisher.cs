using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("publisher")]
public class Publisher
{
    [Key]
    [Column("publisher_id")]
    public long PublisherId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("address")]
    public string? Address { get; set; }

    [MaxLength(30)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(191)]
    [Column("email")]
    public string? Email { get; set; }

    // Navigation properties
    public virtual ICollection<Book> Books { get; set; } = new List<Book>();
    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
