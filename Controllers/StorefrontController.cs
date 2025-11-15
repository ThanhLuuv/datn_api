using System;
using System.Linq;
using System.Threading.Tasks;
using BookStore.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookStore.Api.Models;
using System.Text.RegularExpressions;
namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/storefront")]
public class StorefrontController : ControllerBase
{
    private readonly BookStoreDbContext _db;

    public StorefrontController(BookStoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("effective-price/{isbn}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEffectivePrice(string isbn)
    {
        var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Isbn == isbn);
        if (book == null) return NotFound(new { success = false, message = "ISBN không tồn tại" });

        var latestChange = await _db.PriceChanges.AsNoTracking()
            .Where(pc => pc.Isbn == isbn)
            .OrderByDescending(pc => pc.ChangedAt)
            .FirstOrDefaultAsync();

        var basePrice = book.AveragePrice;
        decimal effective = latestChange != null ? latestChange.NewPrice : basePrice;

        return Ok(new
        {
            success = true,
            data = new
            {
                isbn,
                basePrice,
                effectivePrice = effective,
                source = latestChange != null ? "price_change" : "book"
            }
        });
    }

    // GET: /api/storefront/bestsellers?days=30&top=10
    [HttpGet("bestsellers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBestSellers([FromQuery] int days = 30, [FromQuery] int top = 10)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Abs(days));

        var query = await _db.OrderLines
            .AsNoTracking()
            .Where(ol => ol.Order.PlacedAt >= since)
            .GroupBy(ol => ol.Isbn)
            .Select(g => new { isbn = g.Key, qty = g.Sum(x => x.Qty) })
            .OrderByDescending(x => x.qty)
            .Take(top)
            .Join(_db.Books.AsNoTracking().Where(b => b.Stock > 0 && b.Status == true), g => g.isbn, b => b.Isbn, (g, b) => new
            {
                b.Isbn,
                b.Title,
                b.ImageUrl,
                b.AveragePrice,
                totalSold = g.qty
            })
            .ToListAsync();

        return Ok(new { success = true, data = query });
    }

    // GET: /api/storefront/new-books?days=30&top=10
    [HttpGet("new-books")]
    [AllowAnonymous]
    public async Task<IActionResult> GetNewBooks([FromQuery] int days = 30, [FromQuery] int top = 10)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Abs(days));

        var items = await _db.Books
            .AsNoTracking()
            .Where(b => b.CreatedAt >= since && b.Stock > 0 && b.Status == true)
            .OrderByDescending(b => b.CreatedAt)
            .Take(top)
            .Select(b => new { b.Isbn, b.Title, b.ImageUrl, b.AveragePrice, b.CreatedAt })
            .ToListAsync();

        return Ok(new { success = true, data = items });
    }

    // GET: /api/storefront/search?title=abc&page=1&pageSize=12
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchByTitle([FromQuery] string title = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        var normalizedTitle = NormalizeSearchTerm(title);
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 12;

        var q = _db.Books
            .AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.AuthorBooks)
                .ThenInclude(ab => ab.Author)
            .Include(b => b.BookPromotions)
                .ThenInclude(bp => bp.Promotion)
            .Where(b => b.Stock > 0 && b.Status == true);

        if (!string.IsNullOrEmpty(normalizedTitle))
        {
            // Case-insensitive search with LIKE
            q = q.Where(b => EF.Functions.Like(b.Title, "%" + normalizedTitle + "%"));
        }

        var total = await q.CountAsync();
        var books = await q
            .OrderBy(b => b.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get price changes for all books to calculate current prices
        var bookIsbns = books.Select(b => b.Isbn).ToList();
        var priceChanges = await _db.PriceChanges
            .AsNoTracking()
            .Where(pc => bookIsbns.Contains(pc.Isbn))
            .GroupBy(pc => pc.Isbn)
            .ToDictionaryAsync(g => g.Key, g => g.OrderByDescending(pc => pc.ChangedAt).First());

        var items = books.Select(b => {
            // Calculate current price from PriceChange table
            var currentPrice = priceChanges.ContainsKey(b.Isbn) ? priceChanges[b.Isbn].NewPrice : b.AveragePrice;
            
            // Calculate discounted price
            var discountedPrice = (decimal?)null;
            var hasActivePromotion = b.BookPromotions.Any(bp => 
                bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && 
                bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow));
            
            if (hasActivePromotion)
            {
                var activePromotion = b.BookPromotions
                    .Where(bp => bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && 
                                bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                    .OrderByDescending(bp => bp.Promotion.DiscountPct)
                    .FirstOrDefault();
                
                if (activePromotion != null)
                {
                    discountedPrice = currentPrice * (1 - activePromotion.Promotion.DiscountPct / 100);
                }
            }

            return new
            {
                b.Isbn,
                b.Title,
                b.PageCount,
                b.AveragePrice,
                CurrentPrice = currentPrice,
                DiscountedPrice = discountedPrice,
                b.PublishYear,
                CategoryId = b.CategoryId,
                CategoryName = b.Category?.Name,
                PublisherId = b.PublisherId,
                PublisherName = b.Publisher?.Name,
                b.ImageUrl,
                b.CreatedAt,
                b.UpdatedAt,
                b.Stock,
                b.Status,
                Authors = b.AuthorBooks.Select(ab => new
                {
                    ab.Author.AuthorId,
                    ab.Author.FirstName,
                    ab.Author.LastName,
                    FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                    ab.Author.Gender,
                    ab.Author.DateOfBirth,
                    ab.Author.Address,
                    ab.Author.Email
                }).ToList(),
                ActivePromotions = b.BookPromotions
                    .Where(bp => bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && 
                                bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                    .Select(bp => new
                    {
                        bp.Promotion.PromotionId,
                        bp.Promotion.Name,
                        bp.Promotion.Description,
                        bp.Promotion.DiscountPct,
                        bp.Promotion.StartDate,
                        bp.Promotion.EndDate
                    }).ToList(),
                HasPromotion = hasActivePromotion
            };
        }).ToList();

        return Ok(new { success = true, data = items, pagination = new { page, pageSize, total } });
    }

    // GET: /api/storefront/price-history/{isbn}?limit=20
    [HttpGet("price-history/{isbn}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPriceHistory([FromRoute] string isbn, [FromQuery] int limit = 20)
    {
        isbn = isbn?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(isbn)) return BadRequest(new { success = false, message = "isbn is required" });
        if (limit <= 0 || limit > 200) limit = 20;

        var history = await _db.PriceChanges
            .AsNoTracking()
            .Where(pc => pc.Isbn == isbn)
            .OrderByDescending(pc => pc.ChangedAt)
            .Take(limit)
            .Select(pc => new { pc.Isbn, pc.OldPrice, pc.NewPrice, pc.ChangedAt,                         pc.EmployeeId })
            .ToListAsync();

        return Ok(new { success = true, data = history });
    }

    /// <summary>
    /// Normalizes search term by removing extra whitespaces, trimming, and handling Vietnamese characters
    /// </summary>
    /// <param name="searchTerm">The search term to normalize</param>
    /// <returns>Normalized search term</returns>
    private static string NormalizeSearchTerm(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return string.Empty;

        // Trim and remove extra whitespaces
        var normalized = Regex.Replace(searchTerm.Trim(), @"\s+", " ");
        
        // Remove special characters except Vietnamese characters and basic punctuation
        normalized = Regex.Replace(normalized, @"[^\w\sàáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđĐÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ\-\.]", " ");
        
        // Remove extra spaces again after removing special characters
        normalized = Regex.Replace(normalized, @"\s+", " ");
        
        return normalized.Trim();
    }
}


