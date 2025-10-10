using System;
using System.Linq;
using System.Threading.Tasks;
using BookStore.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookStore.Api.Models;

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
            .Join(_db.Books.AsNoTracking(), g => g.isbn, b => b.Isbn, (g, b) => new
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
    public async Task<IActionResult> GetNewBooks([FromQuery] int days = 30, [FromQuery] int top = 10)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Abs(days));

        var items = await _db.Books
            .AsNoTracking()
            .Where(b => b.CreatedAt >= since)
            .OrderByDescending(b => b.CreatedAt)
            .Take(top)
            .Select(b => new { b.Isbn, b.Title, b.ImageUrl, b.AveragePrice, b.CreatedAt })
            .ToListAsync();

        return Ok(new { success = true, data = items });
    }

    // GET: /api/storefront/search?title=abc&page=1&pageSize=12
    [HttpGet("search")]
    public async Task<IActionResult> SearchByTitle([FromQuery] string title = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        title = title?.Trim() ?? string.Empty;
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 12;

        var q = _db.Books.AsNoTracking();
        if (!string.IsNullOrEmpty(title))
        {
            q = q.Where(b => EF.Functions.Like(b.Title, "%" + title + "%"));
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(b => b.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new { b.Isbn, b.Title, b.ImageUrl, b.AveragePrice })
            .ToListAsync();

        return Ok(new { success = true, data = items, pagination = new { page, pageSize, total } });
    }

    // GET: /api/storefront/price-history/{isbn}?limit=20
    [HttpGet("price-history/{isbn}")]
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
            .Select(pc => new { pc.Isbn, pc.OldPrice, pc.NewPrice, pc.ChangedAt, pc.EmployeeId })
            .ToListAsync();

        return Ok(new { success = true, data = history });
    }
}


