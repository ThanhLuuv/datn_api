using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BookStore.Api.Services;

public class PromotionService : IPromotionService
{
    private readonly BookStoreDbContext _context;
    private readonly ILogger<PromotionService> _logger;

    public PromotionService(BookStoreDbContext context, ILogger<PromotionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<PromotionListResponse>> GetPromotionsAsync(PromotionSearchRequest request)
    {
        try
        {
            var query = _context.Promotions
                .Include(p => p.IssuedByEmployee)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Category)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Publisher)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.Name))
            {
                query = query.Where(p => p.Name.Contains(request.Name));
            }

            if (request.MinDiscountPct.HasValue)
            {
                query = query.Where(p => p.DiscountPct >= request.MinDiscountPct.Value);
            }

            if (request.MaxDiscountPct.HasValue)
            {
                query = query.Where(p => p.DiscountPct <= request.MaxDiscountPct.Value);
            }

            if (request.StartDate.HasValue)
            {
                query = query.Where(p => p.StartDate >= request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(p => p.EndDate <= request.EndDate.Value);
            }

            if (request.IssuedBy.HasValue)
            {
                query = query.Where(p => p.IssuedBy == request.IssuedBy.Value);
            }

            if (!string.IsNullOrEmpty(request.BookIsbn))
            {
                query = query.Where(p => p.BookPromotions.Any(bp => bp.Isbn == request.BookIsbn));
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(request.Status) && request.Status != "all")
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                query = request.Status.ToLower() switch
                {
                    "active" => query.Where(p => p.StartDate <= today && p.EndDate >= today),
                    "upcoming" => query.Where(p => p.StartDate > today),
                    "expired" => query.Where(p => p.EndDate < today),
                    _ => query
                };
            }

            // Apply sorting
            query = ApplySorting(query, request.SortBy, request.SortOrder);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination
            var promotions = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var promotionDtos = promotions.Select(MapToPromotionDto).ToList();

            var result = new PromotionListResponse
            {
                Promotions = promotionDtos,
                TotalCount = totalCount,
                PageNumber = request.Page,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };

            return new ApiResponse<PromotionListResponse>
            {
                Success = true,
                Message = "Lấy danh sách khuyến mãi thành công",
                Data = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting promotions");
            return new ApiResponse<PromotionListResponse>
            {
                Success = false,
                Message = "Lỗi khi lấy danh sách khuyến mãi"
            };
        }
    }

    public async Task<ApiResponse<PromotionDto>> GetPromotionByIdAsync(long promotionId)
    {
        try
        {
            var promotion = await _context.Promotions
                .Include(p => p.IssuedByEmployee)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Category)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Publisher)
                .FirstOrDefaultAsync(p => p.PromotionId == promotionId);

            if (promotion == null)
            {
                return new ApiResponse<PromotionDto>
                {
                    Success = false,
                    Message = "Không tìm thấy khuyến mãi"
                };
            }

            var promotionDto = MapToPromotionDto(promotion);
            return new ApiResponse<PromotionDto>
            {
                Success = true,
                Message = "Lấy thông tin khuyến mãi thành công",
                Data = promotionDto
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting promotion by id {PromotionId}", promotionId);
            return new ApiResponse<PromotionDto>
            {
                Success = false,
                Message = "Lỗi khi lấy thông tin khuyến mãi"
            };
        }
    }

    public async Task<ApiResponse<PromotionDto>> CreatePromotionAsync(CreatePromotionDto createPromotionDto, string createdByEmail)
    {
        try
        {
            // Validate dates
            if (createPromotionDto.EndDate <= createPromotionDto.StartDate)
            {
                return new ApiResponse<PromotionDto> { Success = false, Message = "Ngày kết thúc phải sau ngày bắt đầu" };
            }

            if (createPromotionDto.StartDate < DateOnly.FromDateTime(DateTime.Today))
            {
                return new ApiResponse<PromotionDto> { Success = false, Message = "Ngày bắt đầu không được trong quá khứ" };
            }

            // Get employee by email
            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Account.Email == createdByEmail);

            if (employee == null)
            {
                return new ApiResponse<PromotionDto> { Success = false, Message = "Không tìm thấy thông tin nhân viên" };
            }

            // Check if promotion name already exists
            var existingPromotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Name == createPromotionDto.Name);

            if (existingPromotion != null)
            {
                return new ApiResponse<PromotionDto> { Success = false, Message = "Tên khuyến mãi đã tồn tại" };
            }

            // Validate books exist
            var books = await _context.Books
                .Where(b => createPromotionDto.BookIsbns.Contains(b.Isbn))
                .ToListAsync();

            if (books.Count != createPromotionDto.BookIsbns.Count)
            {
                var missingIsbns = createPromotionDto.BookIsbns.Except(books.Select(b => b.Isbn)).ToList();
                return new ApiResponse<PromotionDto> { Success = false, Message = $"Không tìm thấy sách với ISBN: {string.Join(", ", missingIsbns)}" };
            }

            // Create promotion
            var promotion = new Promotion
            {
                Name = createPromotionDto.Name,
                Description = createPromotionDto.Description,
                DiscountPct = createPromotionDto.DiscountPct,
                StartDate = createPromotionDto.StartDate,
                EndDate = createPromotionDto.EndDate,
                IssuedBy = employee.EmployeeId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            // Create book promotions
            var bookPromotions = createPromotionDto.BookIsbns.Select(isbn => new BookPromotion
            {
                Isbn = isbn,
                PromotionId = promotion.PromotionId
            }).ToList();

            _context.BookPromotions.AddRange(bookPromotions);
            await _context.SaveChangesAsync();

            // Reload promotion with related data
            var createdPromotion = await _context.Promotions
                .Include(p => p.IssuedByEmployee)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Category)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Publisher)
                .FirstAsync(p => p.PromotionId == promotion.PromotionId);

            var promotionDto = MapToPromotionDto(createdPromotion);
            return new ApiResponse<PromotionDto> { Success = true, Message = "Tạo khuyến mãi thành công", Data = promotionDto };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating promotion");
            return new ApiResponse<PromotionDto> { Success = false, Message = "Lỗi khi tạo khuyến mãi" };
        }
    }

    public async Task<ApiResponse<PromotionDto>> UpdatePromotionAsync(long promotionId, UpdatePromotionDto updatePromotionDto, string updatedByEmail)
    {
        try
        {
            var promotion = await _context.Promotions
                .Include(p => p.BookPromotions)
                .FirstOrDefaultAsync(p => p.PromotionId == promotionId);

            if (promotion == null)
            {
                return new ApiResponse<PromotionDto> { Success = false, Message = "Không tìm thấy khuyến mãi" };
            }

            // Check if promotion has started (can't modify started promotions)
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (promotion.StartDate <= today)
            {
                return new ApiResponse<PromotionDto> { Success = false, Message = "Không thể chỉnh sửa khuyến mãi đã bắt đầu" };
            }

            // Validate dates
            if (updatePromotionDto.EndDate <= updatePromotionDto.StartDate)
            {
                return new ApiResponse<PromotionDto> { Success = false, Message = "Ngày kết thúc phải sau ngày bắt đầu" };
            }

            // Check if name already exists (excluding current promotion)
            var existingPromotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Name == updatePromotionDto.Name && p.PromotionId != promotionId);

            if (existingPromotion != null)
            {
                return new ApiResponse<PromotionDto> { Success = false, Message = "Tên khuyến mãi đã tồn tại" };
            }

            // Validate books exist
            var books = await _context.Books
                .Where(b => updatePromotionDto.BookIsbns.Contains(b.Isbn))
                .ToListAsync();

            if (books.Count != updatePromotionDto.BookIsbns.Count)
            {
                var missingIsbns = updatePromotionDto.BookIsbns.Except(books.Select(b => b.Isbn)).ToList();
                return new ApiResponse<PromotionDto> { Success = false, Message = $"Không tìm thấy sách với ISBN: {string.Join(", ", missingIsbns)}" };
            }

            // Update promotion
            promotion.Name = updatePromotionDto.Name;
            promotion.Description = updatePromotionDto.Description;
            promotion.DiscountPct = updatePromotionDto.DiscountPct;
            promotion.StartDate = updatePromotionDto.StartDate;
            promotion.EndDate = updatePromotionDto.EndDate;
            promotion.UpdatedAt = DateTime.UtcNow;

            // Remove existing book promotions
            _context.BookPromotions.RemoveRange(promotion.BookPromotions);

            // Add new book promotions
            var newBookPromotions = updatePromotionDto.BookIsbns.Select(isbn => new BookPromotion
            {
                Isbn = isbn,
                PromotionId = promotion.PromotionId
            }).ToList();

            _context.BookPromotions.AddRange(newBookPromotions);
            await _context.SaveChangesAsync();

            // Reload promotion with related data
            var updatedPromotion = await _context.Promotions
                .Include(p => p.IssuedByEmployee)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Category)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Publisher)
                .FirstAsync(p => p.PromotionId == promotionId);

            var promotionDto = MapToPromotionDto(updatedPromotion);
            return new ApiResponse<PromotionDto> { Success = true, Message = "Cập nhật khuyến mãi thành công", Data = promotionDto };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating promotion {PromotionId}", promotionId);
            return new ApiResponse<PromotionDto> { Success = false, Message = "Lỗi khi cập nhật khuyến mãi" };
        }
    }

    public async Task<ApiResponse<bool>> DeletePromotionAsync(long promotionId, string deletedByEmail)
    {
        try
        {
            var promotion = await _context.Promotions
                .Include(p => p.BookPromotions)
                .FirstOrDefaultAsync(p => p.PromotionId == promotionId);

            if (promotion == null)
            {
                return new ApiResponse<bool> { Success = false, Message = "Không tìm thấy khuyến mãi" };
            }

            // Check if promotion has started (can't delete started promotions)
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (promotion.StartDate <= today)
            {
                return new ApiResponse<bool> { Success = false, Message = "Không thể xóa khuyến mãi đã bắt đầu" };
            }

            // Remove book promotions first (due to foreign key constraint)
            _context.BookPromotions.RemoveRange(promotion.BookPromotions);
            
            // Remove promotion
            _context.Promotions.Remove(promotion);
            
            await _context.SaveChangesAsync();

            return new ApiResponse<bool> { Success = true, Message = "Xóa khuyến mãi thành công", Data = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting promotion {PromotionId}", promotionId);
            return new ApiResponse<bool> { Success = false, Message = "Lỗi khi xóa khuyến mãi" };
        }
    }

    public async Task<ApiResponse<PromotionStatsDto>> GetPromotionStatsAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            
            var totalPromotions = await _context.Promotions.CountAsync();
            var activePromotions = await _context.Promotions
                .CountAsync(p => p.StartDate <= today && p.EndDate >= today);
            var upcomingPromotions = await _context.Promotions
                .CountAsync(p => p.StartDate > today);
            var expiredPromotions = await _context.Promotions
                .CountAsync(p => p.EndDate < today);

            var averageDiscountPct = totalPromotions > 0 
                ? await _context.Promotions.AverageAsync(p => p.DiscountPct)
                : 0;

            var totalBooksInPromotion = await _context.BookPromotions
                .Join(_context.Promotions, bp => bp.PromotionId, p => p.PromotionId, (bp, p) => new { bp, p })
                .Where(x => x.p.StartDate <= today && x.p.EndDate >= today)
                .Select(x => x.bp.Isbn)
                .Distinct()
                .CountAsync();

            var stats = new PromotionStatsDto
            {
                TotalPromotions = totalPromotions,
                ActivePromotions = activePromotions,
                UpcomingPromotions = upcomingPromotions,
                ExpiredPromotions = expiredPromotions,
                AverageDiscountPct = averageDiscountPct,
                TotalBooksInPromotion = totalBooksInPromotion
            };

            return new ApiResponse<PromotionStatsDto> { Success = true, Message = "Lấy thống kê khuyến mãi thành công", Data = stats };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting promotion stats");
            return new ApiResponse<PromotionStatsDto> { Success = false, Message = "Lỗi khi lấy thống kê khuyến mãi" };
        }
    }

    public async Task<ApiResponse<List<PromotionBookDto>>> GetActivePromotionBooksAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            
            var activePromotionBooks = await _context.BookPromotions
                .Include(bp => bp.Book)
                    .ThenInclude(b => b.Category)
                .Include(bp => bp.Book)
                    .ThenInclude(b => b.Publisher)
                .Include(bp => bp.Promotion)
                .Where(bp => bp.Promotion.StartDate <= today && bp.Promotion.EndDate >= today)
                .ToListAsync();

            var bookPromotionDtos = activePromotionBooks.Select(bp => new PromotionBookDto
            {
                Isbn = bp.Isbn,
                Title = bp.Book.Title,
                UnitPrice = bp.Book.AveragePrice,
                DiscountedPrice = bp.Book.AveragePrice * (1 - bp.Promotion.DiscountPct / 100),
                CategoryName = bp.Book.Category.Name,
                PublisherName = bp.Book.Publisher.Name
            }).ToList();

            return new ApiResponse<List<PromotionBookDto>>
            {
                Success = true,
                Message = "Lấy danh sách sách khuyến mãi thành công",
                Data = bookPromotionDtos
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active promotion books");
            return new ApiResponse<List<PromotionBookDto>>
            {
                Success = false,
                Message = "Lỗi khi lấy danh sách sách khuyến mãi"
            };
        }
    }

    public async Task<ApiResponse<List<PromotionDto>>> GetActivePromotionsForBookAsync(string isbn)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            
            var activePromotions = await _context.Promotions
                .Include(p => p.IssuedByEmployee)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Category)
                .Include(p => p.BookPromotions)
                    .ThenInclude(bp => bp.Book)
                        .ThenInclude(b => b.Publisher)
                .Where(p => p.BookPromotions.Any(bp => bp.Isbn == isbn) &&
                           p.StartDate <= today && p.EndDate >= today)
                .ToListAsync();

            var promotionDtos = activePromotions.Select(MapToPromotionDto).ToList();
            return new ApiResponse<List<PromotionDto>>
            {
                Success = true,
                Message = "Lấy danh sách khuyến mãi theo sách thành công",
                Data = promotionDtos
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active promotions for book {Isbn}", isbn);
            return new ApiResponse<List<PromotionDto>>
            {
                Success = false,
                Message = "Lỗi khi lấy danh sách khuyến mãi cho sách"
            };
        }
    }

    private static IQueryable<Promotion> ApplySorting(IQueryable<Promotion> query, string sortBy, string sortOrder)
    {
        Expression<Func<Promotion, object>> keySelector = sortBy.ToLower() switch
        {
            "name" => p => p.Name,
            "discountpct" => p => p.DiscountPct,
            "startdate" => p => p.StartDate,
            "enddate" => p => p.EndDate,
            "updatedat" => p => p.UpdatedAt,
            _ => p => p.CreatedAt
        };

        return sortOrder.ToLower() == "asc" 
            ? query.OrderBy(keySelector) 
            : query.OrderByDescending(keySelector);
    }

    private static PromotionDto MapToPromotionDto(Promotion promotion)
    {
        return new PromotionDto
        {
            PromotionId = promotion.PromotionId,
            Name = promotion.Name,
            Description = promotion.Description,
            DiscountPct = promotion.DiscountPct,
            StartDate = promotion.StartDate,
            EndDate = promotion.EndDate,
            IssuedBy = promotion.IssuedBy,
            IssuedByName = $"{promotion.IssuedByEmployee.FirstName} {promotion.IssuedByEmployee.LastName}",
            CreatedAt = promotion.CreatedAt,
            UpdatedAt = promotion.UpdatedAt,
            Books = promotion.BookPromotions.Select(bp => new PromotionBookDto
            {
                Isbn = bp.Isbn,
                Title = bp.Book.Title,
                UnitPrice = bp.Book.AveragePrice,
                DiscountedPrice = bp.Book.AveragePrice * (1 - promotion.DiscountPct / 100),
                CategoryName = bp.Book.Category.Name,
                PublisherName = bp.Book.Publisher.Name
            }).ToList()
        };
    }
}
