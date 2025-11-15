using System.Security.Claims;
using BookStore.Api.Data;
using BookStore.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/ratings")]
public class RatingsController : ControllerBase
{
    private readonly BookStoreDbContext _db;

    public RatingsController(BookStoreDbContext db)
    {
        _db = db;
    }

    public class CreateOrUpdateRatingDto
    {
        public string Isbn { get; set; } = string.Empty;
        public int Stars { get; set; }
        public string? Comment { get; set; }
    }

    [HttpGet("{isbn}")]
    [AllowAnonymous]
    public async Task<IActionResult> ListByIsbn(string isbn, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Ratings.AsNoTracking().Where(r => r.Isbn == isbn).OrderByDescending(r => r.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var avg = total > 0 ? await _db.Ratings.Where(r => r.Isbn == isbn).AverageAsync(r => (double)r.Stars) : 0.0;

        return Ok(new { success = true, data = items, total, page, pageSize, avgStars = Math.Round(avg, 2) });
    }

    [HttpGet("{isbn}/stats")]
    [AllowAnonymous]
    public async Task<IActionResult> Stats(string isbn)
    {
        var ratings = _db.Ratings.AsNoTracking().Where(r => r.Isbn == isbn);
        var total = await ratings.CountAsync();
        var avg = total > 0 ? await ratings.AverageAsync(r => (double)r.Stars) : 0.0;
        var histogram = await ratings.GroupBy(r => r.Stars).Select(g => new { stars = g.Key, count = g.Count() }).ToListAsync();
        return Ok(new { success = true, total, avgStars = Math.Round(avg, 2), histogram });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateOrUpdate([FromBody] CreateOrUpdateRatingDto dto)
    {
        if (dto.Stars < 1 || dto.Stars > 5) return BadRequest(new { success = false, message = "Stars must be 1..5" });
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (email == null) return Unauthorized();

        var customerId = await _db.Accounts
            .Where(a => a.Email == email)
            .Join(_db.Customers, a => a.AccountId, c => c.AccountId, (a, c) => c.CustomerId)
            .SingleOrDefaultAsync();
        if (customerId == 0) return Forbid();

        // Purchase verification: customer must have at least one delivered order containing this ISBN
        var purchased = await _db.OrderLines
            .Include(ol => ol.Order)
            .AnyAsync(ol => ol.Isbn == dto.Isbn && ol.Order.CustomerId == customerId && ol.Order.Status == OrderStatus.Delivered);

        if (!purchased)
            return Forbid("Bạn chỉ có thể đánh giá sách đã mua và giao thành công.");

        var now = DateTime.UtcNow;
        var existing = await _db.Ratings.FirstOrDefaultAsync(r => r.Isbn == dto.Isbn && r.CustomerId == customerId);
        if (existing == null)
        {
            var rating = new Rating
            {
                CustomerId = customerId,
                Isbn = dto.Isbn,
                Stars = dto.Stars,
                Comment = dto.Comment,
                CreatedAt = now
            };
            _db.Ratings.Add(rating);
        }
        else
        {
            existing.Stars = dto.Stars;
            existing.Comment = dto.Comment;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}


