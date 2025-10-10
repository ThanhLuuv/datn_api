using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class PriceChangeService : IPriceChangeService
{
    private readonly BookStoreDbContext _context;

    public PriceChangeService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<PriceChangeListResponse>> GetPriceChangesAsync(PriceChangeSearchRequest request)
    {
        try
        {
            var query = _context.PriceChanges
                .Include(pc => pc.Book)
                .Include(pc => pc.Employee)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.Isbn))
            {
                query = query.Where(pc => pc.Isbn.Contains(request.Isbn));
            }

            if (request.EmployeeId.HasValue)
            {
                query = query.Where(pc => pc.EmployeeId == request.EmployeeId.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(pc => pc.ChangedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(pc => pc.ChangedAt <= request.ToDate.Value);
            }

            // IsActive no longer used with composite key; filter by dates instead

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

            var priceChanges = await query
                .OrderByDescending(pc => pc.ChangedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(pc => new PriceChangeDto
                {
                    PriceChangeId = 0,
                    Isbn = pc.Isbn,
                    BookTitle = pc.Book.Title,
                    OldPrice = pc.OldPrice,
                    NewPrice = pc.NewPrice,
                    EffectiveDate = pc.ChangedAt,
                    ChangedAt = pc.ChangedAt,
                    EmployeeId = pc.EmployeeId,
                    EmployeeName = pc.Employee.FirstName + " " + pc.Employee.LastName,
                    IsActive = null
                })
                .ToListAsync();

            var response = new PriceChangeListResponse
            {
                PriceChanges = priceChanges,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<PriceChangeListResponse>
            {
                Success = true,
                Message = "Get price changes successfully",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PriceChangeListResponse>
            {
                Success = false,
                Message = "Error getting price changes",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<PriceChangeDto>> GetPriceChangeByIdAsync(long priceChangeId)
    {
        try
        {
            var priceChange = await _context.PriceChanges
                .Include(pc => pc.Book)
                .Include(pc => pc.Employee)
                .OrderByDescending(pc => pc.ChangedAt)
                .Select(pc => new PriceChangeDto
                {
                    PriceChangeId = 0,
                    Isbn = pc.Isbn,
                    BookTitle = pc.Book.Title,
                    OldPrice = pc.OldPrice,
                    NewPrice = pc.NewPrice,
                    EffectiveDate = pc.ChangedAt,
                    ChangedAt = pc.ChangedAt,
                    EmployeeId = pc.EmployeeId,
                    EmployeeName = pc.Employee.FirstName + " " + pc.Employee.LastName,
                    IsActive = null
                })
                .FirstOrDefaultAsync();

            if (priceChange == null)
            {
                return new ApiResponse<PriceChangeDto>
                {
                    Success = false,
                    Message = "Price change not found",
                    Errors = new List<string> { "Price change does not exist" }
                };
            }

            return new ApiResponse<PriceChangeDto>
            {
                Success = true,
                Message = "Get price change successfully",
                Data = priceChange
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PriceChangeDto>
            {
                Success = false,
                Message = "Error getting price change",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<PriceChangeDto>> CreatePriceChangeAsync(CreatePriceChangeDto createPriceChangeDto, long employeeAccountId)
    {
        try
        {
            // Validate book exists
            var book = await _context.Books.FindAsync(createPriceChangeDto.Isbn);
            if (book == null)
            {
                return new ApiResponse<PriceChangeDto>
                {
                    Success = false,
                    Message = "Book not found",
                    Errors = new List<string> { "Book does not exist" }
                };
            }

            // Get employee from account
            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.AccountId == employeeAccountId);
            
            if (employee == null)
            {
                return new ApiResponse<PriceChangeDto>
                {
                    Success = false,
                    Message = "Employee not found",
                    Errors = new List<string> { "Employee does not exist" }
                };
            }

            // Get current price to use as old price
            var currentPriceResult = await GetCurrentPriceAsync(createPriceChangeDto.Isbn);
            var oldPrice = currentPriceResult.Success ? currentPriceResult.Data : book.AveragePrice;

            // Create price change
            var priceChange = new PriceChange
            {
                Isbn = createPriceChangeDto.Isbn,
                OldPrice = oldPrice,
                NewPrice = createPriceChangeDto.NewPrice,
                ChangedAt = DateTime.UtcNow,
                EmployeeId = employee.EmployeeId,
                // Reason omitted (no column in DB)
            };

            _context.PriceChanges.Add(priceChange);
            await _context.SaveChangesAsync();

            // Update book average price
            await UpdateBookAveragePriceAsync(createPriceChangeDto.Isbn);

            // Return the created price change
            return new ApiResponse<PriceChangeDto>
            {
                Success = true,
                Message = "Price change created",
                Data = new PriceChangeDto
                {
                    PriceChangeId = 0,
                    Isbn = priceChange.Isbn,
                    BookTitle = book.Title,
                    OldPrice = priceChange.OldPrice,
                    NewPrice = priceChange.NewPrice,
                    EffectiveDate = priceChange.ChangedAt,
                    ChangedAt = priceChange.ChangedAt,
                    EmployeeId = priceChange.EmployeeId,
                    EmployeeName = employee.FirstName + " " + employee.LastName,
                    IsActive = null
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PriceChangeDto>
            {
                Success = false,
                Message = "Error creating price change",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<decimal>> GetCurrentPriceAsync(string isbn, DateTime? asOfDate = null)
    {
        try
        {
            var effectiveDate = asOfDate ?? DateTime.UtcNow;
            
            var currentPriceChange = await _context.PriceChanges
                .Where(pc => pc.Isbn == isbn && pc.ChangedAt <= effectiveDate)
                .OrderByDescending(pc => pc.ChangedAt)
                .FirstOrDefaultAsync();

            decimal currentPrice;
            if (currentPriceChange != null)
            {
                currentPrice = currentPriceChange.NewPrice;
            }
            else
            {
                // Fallback to average price from book table
                var book = await _context.Books
                    .Where(b => b.Isbn == isbn)
                    .Select(b => b.AveragePrice)
                    .FirstOrDefaultAsync();
                
                currentPrice = book;
            }

            return new ApiResponse<decimal>
            {
                Success = true,
                Message = "Get current price successfully",
                Data = currentPrice
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<decimal>
            {
                Success = false,
                Message = "Error getting current price",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<PriceChangeDto>>> GetPriceHistoryAsync(string isbn)
    {
        try
        {
            var priceHistory = await _context.PriceChanges
                .Include(pc => pc.Book)
                .Include(pc => pc.Employee)
                .Where(pc => pc.Isbn == isbn)
                .OrderByDescending(pc => pc.ChangedAt)
                .Select(pc => new PriceChangeDto
                {
                    PriceChangeId = 0,
                    Isbn = pc.Isbn,
                    BookTitle = pc.Book.Title,
                    OldPrice = pc.OldPrice,
                    NewPrice = pc.NewPrice,
                    EffectiveDate = pc.ChangedAt,
                    ChangedAt = pc.ChangedAt,
                    EmployeeId = pc.EmployeeId,
                    EmployeeName = pc.Employee.FirstName + " " + pc.Employee.LastName,
                    IsActive = null
                })
                .ToListAsync();

            return new ApiResponse<List<PriceChangeDto>>
            {
                Success = true,
                Message = "Get price history successfully",
                Data = priceHistory
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<PriceChangeDto>>
            {
                Success = false,
                Message = "Error getting price history",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private async Task UpdateBookAveragePriceAsync(string isbn)
    {
        try
        {
            var avgPrice = await _context.PriceChanges
                .Where(pc => pc.Isbn == isbn)
                .AverageAsync(pc => pc.NewPrice);

            var book = await _context.Books.FindAsync(isbn);
            if (book != null)
            {
                book.AveragePrice = avgPrice;
                book.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - this is a background operation
            Console.WriteLine($"Error updating average price for {isbn}: {ex.Message}");
        }
    }
}
