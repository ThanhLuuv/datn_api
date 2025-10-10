using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

public class Cart
{
    [Key]
    [Column("cart_id")]
    public long CartId { get; set; }

    [Required]
    [Column("customer_id")]
    public long CustomerId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Customer Customer { get; set; } = null!;
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}

public class CartItem
{
    [Key]
    [Column("cart_item_id")]
    public long CartItemId { get; set; }

    [Required]
    [Column("cart_id")]
    public long CartId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("isbn")]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("added_at")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Cart Cart { get; set; } = null!;
    public Book Book { get; set; } = null!;
}
