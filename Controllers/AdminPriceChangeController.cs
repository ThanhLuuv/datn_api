using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BookStore.Api.Data;
using BookStore.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/admin/price-changes")] 
[Authorize(Policy = "PERM_READ_PRICE_CHANGE")]
public class AdminPriceChangeController : ControllerBase
{
    private readonly BookStoreDbContext _db;

    public AdminPriceChangeController(BookStoreDbContext db)
    {
        _db = db;
    }

    // GET: /api/admin/price-changes?isbn=&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? isbn, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 20;

        var q = _db.PriceChanges.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(isbn))
        {
            q = q.Where(pc => pc.Isbn == isbn);
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(pc => pc.ChangedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { success = true, data = items, pagination = new { page, pageSize, total } });
    }

    public class CreatePriceChangeDto
    {
        public string Isbn { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime? ChangedAt { get; set; }
        public long EmployeeId { get; set; }
    }

    // POST: /api/admin/price-changes
    [HttpPost]
    [Authorize(Policy = "PERM_WRITE_PRICE_CHANGE")]
    public async Task<IActionResult> Create([FromBody] CreatePriceChangeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Isbn)) return BadRequest(new { success = false, message = "isbn is required" });
        if (dto.NewPrice < 0 || dto.OldPrice < 0) return BadRequest(new { success = false, message = "price must be >= 0" });

        // optional: validate book exists
        var existsBook = await _db.Books.AsNoTracking().AnyAsync(b => b.Isbn == dto.Isbn);
        if (!existsBook) return NotFound(new { success = false, message = "Book not found" });

        var entity = new PriceChange
        {
            Isbn = dto.Isbn.Trim(),
            OldPrice = dto.OldPrice,
            NewPrice = dto.NewPrice,
            ChangedAt = dto.ChangedAt?.ToUniversalTime() ?? DateTime.UtcNow,
            EmployeeId = dto.EmployeeId
        };

        _db.PriceChanges.Add(entity);
        await _db.SaveChangesAsync();

        LogToFile("CREATE", entity);
        return Ok(new { success = true, data = entity });
    }

    public class UpdatePriceChangeDto
    {
        public decimal? OldPrice { get; set; }
        public decimal? NewPrice { get; set; }
        public long? EmployeeId { get; set; }
    }

    // PUT: /api/admin/price-changes/{isbn}/{changedAt}
    [HttpPut("{isbn}/{changedAt}")]
    [Authorize(Policy = "PERM_WRITE_PRICE_CHANGE")]
    public async Task<IActionResult> Update([FromRoute] string isbn, [FromRoute] DateTime changedAt, [FromBody] UpdatePriceChangeDto dto)
    {
        var entity = await _db.PriceChanges.FirstOrDefaultAsync(pc => pc.Isbn == isbn && pc.ChangedAt == changedAt);
        if (entity == null) return NotFound(new { success = false, message = "Price change not found" });

        var before = new PriceChange { Isbn = entity.Isbn, OldPrice = entity.OldPrice, NewPrice = entity.NewPrice, ChangedAt = entity.ChangedAt, EmployeeId = entity.EmployeeId };

        if (dto.OldPrice.HasValue)
        {
            if (dto.OldPrice.Value < 0) return BadRequest(new { success = false, message = "oldPrice must be >= 0" });
            entity.OldPrice = dto.OldPrice.Value;
        }
        if (dto.NewPrice.HasValue)
        {
            if (dto.NewPrice.Value < 0) return BadRequest(new { success = false, message = "newPrice must be >= 0" });
            entity.NewPrice = dto.NewPrice.Value;
        }
        if (dto.EmployeeId.HasValue)
        {
            entity.EmployeeId = dto.EmployeeId.Value;
        }

        await _db.SaveChangesAsync();
        LogToFile("UPDATE", entity, before);
        return Ok(new { success = true, data = entity });
    }

    // DELETE: /api/admin/price-changes/{isbn}/{changedAt}
    [HttpDelete("{isbn}/{changedAt}")]
    [Authorize(Policy = "PERM_WRITE_PRICE_CHANGE")]
    public async Task<IActionResult> Delete([FromRoute] string isbn, [FromRoute] DateTime changedAt)
    {
        var entity = await _db.PriceChanges.FirstOrDefaultAsync(pc => pc.Isbn == isbn && pc.ChangedAt == changedAt);
        if (entity == null) return NotFound(new { success = false, message = "Price change not found" });

        _db.PriceChanges.Remove(entity);
        await _db.SaveChangesAsync();
        LogToFile("DELETE", entity);
        return Ok(new { success = true });
    }

    private void LogToFile(string action, PriceChange current, PriceChange? before = null)
    {
        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, "pricechange.log");

            var payload = new
            {
                action,
                at = DateTime.UtcNow,
                actor = User?.Identity?.Name ?? "system",
                before,
                after = current
            };
            var line = JsonSerializer.Serialize(payload);
            System.IO.File.AppendAllText(logFile, line + Environment.NewLine);
        }
        catch
        {
            // ignore logging errors
        }
    }
}


