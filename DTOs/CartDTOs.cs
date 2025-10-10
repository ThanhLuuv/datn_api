using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class AddToCartRequestDto
{
    [Required]
    [MaxLength(20)]
    public string Isbn { get; set; } = string.Empty;

    [Required]
    [Range(1, 999)]
    public int Quantity { get; set; }
}

public class UpdateCartItemRequestDto
{
    [Required]
    [Range(1, 999)]
    public int Quantity { get; set; }
}

public class CartItemDto
{
    public long CartItemId { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string PublisherName { get; set; } = string.Empty;
    public int Stock { get; set; }
    public bool HasPromotion { get; set; }
    public List<PromotionDto> ActivePromotions { get; set; } = new List<PromotionDto>();
    public DateTime AddedAt { get; set; }
}

public class CartDto
{
    public long CartId { get; set; }
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
    public int TotalItems { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CartSummaryDto
{
    public int TotalItems { get; set; }
    public decimal TotalAmount { get; set; }
    public int UniqueBooks { get; set; }
}
