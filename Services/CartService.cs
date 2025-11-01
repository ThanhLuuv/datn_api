using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class CartService : ICartService
{
    private readonly BookStoreDbContext _context;
    private readonly IBookService _bookService;

    public CartService(BookStoreDbContext context, IBookService bookService)
    {
        _context = context;
        _bookService = bookService;
    }

    public async Task<ApiResponse<CartDto>> GetCartAsync(long customerId)
    {
        try
        {
            var cart = await _context.Carts
                .Include(c => c.Customer)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Book)
                        .ThenInclude(b => b.Category)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Book)
                        .ThenInclude(b => b.Publisher)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Book)
                        .ThenInclude(b => b.BookPromotions)
                            .ThenInclude(bp => bp.Promotion)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                // Create empty cart if not exists
                cart = new Cart
                {
                    CustomerId = customerId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var cartDto = await MapToCartDtoAsync(cart);
            return new ApiResponse<CartDto>
            {
                Success = true,
                Message = "Lấy giỏ hàng thành công",
                Data = cartDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CartDto>
            {
                Success = false,
                Message = "Lỗi khi lấy giỏ hàng",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<CartSummaryDto>> GetCartSummaryAsync(long customerId)
    {
        try
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                return new ApiResponse<CartSummaryDto>
                {
                    Success = true,
                    Message = "Giỏ hàng trống",
                    Data = new CartSummaryDto
                    {
                        TotalItems = 0,
                        TotalAmount = 0,
                        UniqueBooks = 0
                    }
                };
            }

            var summary = new CartSummaryDto
            {
                TotalItems = cart.CartItems.Sum(ci => ci.Quantity),
                UniqueBooks = cart.CartItems.Count,
                TotalAmount = 0 // Will be calculated with current prices
            };

            // Calculate total with current prices
            foreach (var item in cart.CartItems)
            {
                var currentPrice = await GetCurrentPriceAsync(item.Isbn);
                summary.TotalAmount += currentPrice * item.Quantity;
            }

            return new ApiResponse<CartSummaryDto>
            {
                Success = true,
                Message = "Lấy tóm tắt giỏ hàng thành công",
                Data = summary
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CartSummaryDto>
            {
                Success = false,
                Message = "Lỗi khi lấy tóm tắt giỏ hàng",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<CartItemDto>> AddToCartAsync(long customerId, AddToCartRequestDto request)
    {
        try
        {
            // Validate book exists
            var book = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .FirstOrDefaultAsync(b => b.Isbn == request.Isbn);

            if (book == null)
            {
                return new ApiResponse<CartItemDto>
                {
                    Success = false,
                    Message = "Sách không tồn tại",
                    Errors = new List<string> { $"Sách với ISBN {request.Isbn} không tồn tại" }
                };
            }

            // Check stock
            if (book.Stock < request.Quantity)
            {
                return new ApiResponse<CartItemDto>
                {
                    Success = false,
                    Message = "Không đủ tồn kho",
                    Errors = new List<string> { $"Chỉ còn {book.Stock} cuốn trong kho" }
                };
            }

            // Get or create cart
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerId = customerId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            // Consolidate any duplicate rows of the same ISBN (defensive fix for legacy data)
            var sameIsbnItems = cart.CartItems.Where(ci => ci.Isbn == request.Isbn).ToList();
            if (sameIsbnItems.Count > 1)
            {
                var keepItem = sameIsbnItems[0];
                var mergedQty = sameIsbnItems.Skip(1).Sum(x => x.Quantity);
                if (mergedQty > 0)
                {
                    keepItem.Quantity += mergedQty;
                    keepItem.UpdatedAt = DateTime.UtcNow;
                }
                _context.CartItems.RemoveRange(sameIsbnItems.Skip(1));
            }

            // Check if item already exists
            var existingItem = cart.CartItems.FirstOrDefault(ci => ci.Isbn == request.Isbn);
            if (existingItem != null)
            {
                // Update quantity
                existingItem.Quantity += request.Quantity;
                existingItem.UpdatedAt = DateTime.UtcNow;
                
                // Check total stock
                if (existingItem.Quantity > book.Stock)
                {
                    return new ApiResponse<CartItemDto>
                    {
                        Success = false,
                        Message = "Không đủ tồn kho",
                        Errors = new List<string> { $"Tổng số lượng vượt quá tồn kho ({book.Stock} cuốn)" }
                    };
                }
            }
            else
            {
                // Add new item
                var cartItem = new CartItem
                {
                    CartId = cart.CartId,
                    Isbn = request.Isbn,
                    Quantity = request.Quantity,
                    AddedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.CartItems.Add(cartItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Get updated cart item
            var updatedItem = await _context.CartItems
                .Include(ci => ci.Book)
                    .ThenInclude(b => b.Category)
                .Include(ci => ci.Book)
                    .ThenInclude(b => b.Publisher)
                .Include(ci => ci.Book)
                    .ThenInclude(b => b.BookPromotions)
                        .ThenInclude(bp => bp.Promotion)
                .FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.Isbn == request.Isbn);

            var cartItemDto = await MapToCartItemDtoAsync(updatedItem!);
            return new ApiResponse<CartItemDto>
            {
                Success = true,
                Message = "Thêm vào giỏ hàng thành công",
                Data = cartItemDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CartItemDto>
            {
                Success = false,
                Message = "Lỗi khi thêm vào giỏ hàng",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<CartItemDto>> UpdateCartItemAsync(long customerId, long cartItemId, UpdateCartItemRequestDto request)
    {
        try
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .Include(ci => ci.Book)
                .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.Cart.CustomerId == customerId);

            if (cartItem == null)
            {
                return new ApiResponse<CartItemDto>
                {
                    Success = false,
                    Message = "Sản phẩm không tồn tại trong giỏ hàng",
                    Errors = new List<string> { "Không tìm thấy sản phẩm trong giỏ hàng" }
                };
            }

            // Check stock
            if (request.Quantity > cartItem.Book.Stock)
            {
                return new ApiResponse<CartItemDto>
                {
                    Success = false,
                    Message = "Không đủ tồn kho",
                    Errors = new List<string> { $"Chỉ còn {cartItem.Book.Stock} cuốn trong kho" }
                };
            }

            cartItem.Quantity = request.Quantity;
            cartItem.UpdatedAt = DateTime.UtcNow;
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Get updated cart item with full details
            var updatedItem = await _context.CartItems
                .Include(ci => ci.Book)
                    .ThenInclude(b => b.Category)
                .Include(ci => ci.Book)
                    .ThenInclude(b => b.Publisher)
                .Include(ci => ci.Book)
                    .ThenInclude(b => b.BookPromotions)
                        .ThenInclude(bp => bp.Promotion)
                .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId);

            var cartItemDto = await MapToCartItemDtoAsync(updatedItem!);
            return new ApiResponse<CartItemDto>
            {
                Success = true,
                Message = "Cập nhật giỏ hàng thành công",
                Data = cartItemDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CartItemDto>
            {
                Success = false,
                Message = "Lỗi khi cập nhật giỏ hàng",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> RemoveFromCartAsync(long customerId, long cartItemId)
    {
        try
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.Cart.CustomerId == customerId);

            if (cartItem == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Sản phẩm không tồn tại trong giỏ hàng",
                    Errors = new List<string> { "Không tìm thấy sản phẩm trong giỏ hàng" }
                };
            }

            _context.CartItems.Remove(cartItem);
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Xóa sản phẩm khỏi giỏ hàng thành công",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Lỗi khi xóa sản phẩm khỏi giỏ hàng",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> ClearCartAsync(long customerId)
    {
        try
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null || !cart.CartItems.Any())
            {
                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Giỏ hàng đã trống",
                    Data = true
                };
            }

            _context.CartItems.RemoveRange(cart.CartItems);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Xóa tất cả sản phẩm khỏi giỏ hàng thành công",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Lỗi khi xóa giỏ hàng",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> RemoveBookFromCartAsync(long customerId, string isbn)
    {
        try
        {
            var cartItems = await _context.CartItems
                .Include(ci => ci.Cart)
                .Where(ci => ci.Isbn == isbn && ci.Cart.CustomerId == customerId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Sách không tồn tại trong giỏ hàng",
                    Errors = new List<string> { $"Sách với ISBN {isbn} không có trong giỏ hàng" }
                };
            }

            _context.CartItems.RemoveRange(cartItems);
            foreach (var cart in cartItems.Select(ci => ci.Cart).Distinct())
            {
                cart.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Xóa sách khỏi giỏ hàng thành công",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Lỗi khi xóa sách khỏi giỏ hàng",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private async Task<CartDto> MapToCartDtoAsync(Cart cart)
    {
        var cartDto = new CartDto
        {
            CartId = cart.CartId,
            CustomerId = cart.CustomerId,
            CustomerName = $"{cart.Customer.FirstName} {cart.Customer.LastName}",
            CreatedAt = cart.CreatedAt,
            UpdatedAt = cart.UpdatedAt
        };

        foreach (var item in cart.CartItems)
        {
            var cartItemDto = await MapToCartItemDtoAsync(item);
            cartDto.Items.Add(cartItemDto);
        }

        cartDto.TotalItems = cartDto.Items.Sum(i => i.Quantity);
        cartDto.TotalAmount = cartDto.Items.Sum(i => i.TotalPrice);

        return cartDto;
    }

    private async Task<CartItemDto> MapToCartItemDtoAsync(CartItem item)
    {
        var currentPrice = await GetCurrentPriceAsync(item.Isbn);
        var discountedPrice = await GetDiscountedPriceAsync(item.Isbn);
        
        var cartItemDto = new CartItemDto
        {
            CartItemId = item.CartItemId,
            Isbn = item.Isbn,
            BookTitle = item.Book.Title,
            UnitPrice = item.Book.AveragePrice,
            CurrentPrice = currentPrice,
            DiscountedPrice = discountedPrice,
            Quantity = item.Quantity,
            TotalPrice = discountedPrice * item.Quantity,
            ImageUrl = item.Book.ImageUrl ?? "",
            CategoryName = item.Book.Category.Name,
            PublisherName = item.Book.Publisher.Name,
            Stock = item.Book.Stock,
            AddedAt = item.AddedAt
        };

        // Check for active promotions
        var activePromotions = item.Book.BookPromotions
            .Where(bp => bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) &&
                        bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            .Select(bp => new PromotionDto
            {
                PromotionId = bp.Promotion.PromotionId,
                Name = bp.Promotion.Name,
                DiscountPct = bp.Promotion.DiscountPct,
                StartDate = bp.Promotion.StartDate,
                EndDate = bp.Promotion.EndDate
            })
            .ToList();

        cartItemDto.HasPromotion = activePromotions.Any();
        cartItemDto.ActivePromotions = activePromotions;

        return cartItemDto;
    }

    public async Task<Customer?> GetCustomerByAccountIdAsync(long accountId)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.AccountId == accountId);
    }

    private async Task<decimal> GetCurrentPriceAsync(string isbn)
    {
        // Get current price from price_change table or use average_price
        var currentPriceChange = await _context.PriceChanges
            .Where(pc => pc.Isbn == isbn)
            .OrderByDescending(pc => pc.ChangedAt)
            .FirstOrDefaultAsync();

        if (currentPriceChange != null)
        {
            return currentPriceChange.NewPrice;
        }

        // Fallback to book's average price
        var book = await _context.Books
            .Where(b => b.Isbn == isbn)
            .Select(b => b.AveragePrice)
            .FirstOrDefaultAsync();

        return book;
    }

    private async Task<decimal> GetDiscountedPriceAsync(string isbn)
    {
        var currentPrice = await GetCurrentPriceAsync(isbn);
        
        // Check for active promotions
        var activePromotion = await _context.BookPromotions
            .Include(bp => bp.Promotion)
            .Where(bp => bp.Isbn == isbn &&
                        bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) &&
                        bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            .OrderByDescending(bp => bp.Promotion.DiscountPct)
            .FirstOrDefaultAsync();

        if (activePromotion != null)
        {
            return currentPrice * (1 - activePromotion.Promotion.DiscountPct / 100);
        }

        return currentPrice;
    }
}
