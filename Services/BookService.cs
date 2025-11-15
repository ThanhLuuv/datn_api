using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace BookStore.Api.Services;

public class BookService : IBookService
{
    private readonly BookStoreDbContext _context;
    private readonly IConfiguration _configuration;

    public BookService(BookStoreDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private async Task<decimal> GetCurrentPriceAsync(string isbn, DateTime? asOfDate = null)
    {
        var effectiveDate = asOfDate ?? DateTime.UtcNow;
        
            var currentPriceChange = await _context.PriceChanges
            .Where(pc => pc.Isbn == isbn && pc.ChangedAt <= effectiveDate)
            .OrderByDescending(pc => pc.ChangedAt)
            .FirstOrDefaultAsync();

        if (currentPriceChange != null)
        {
            return currentPriceChange.NewPrice;
        }

        // Fallback to average price from book table
        var book = await _context.Books
            .Where(b => b.Isbn == isbn)
            .Select(b => b.AveragePrice)
            .FirstOrDefaultAsync();

        return book;
    }

    public async Task<ApiResponse<BookListResponse>> GetBooksAsync(BookSearchRequest searchRequest)
    {
        try
        {
            var query = _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Include(b => b.BookPromotions)
                    .ThenInclude(bp => bp.Promotion)
                .AsQueryable();

            // Apply search filters
            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                var normalizedSearchTerm = NormalizeSearchTerm(searchRequest.SearchTerm);
                if (!string.IsNullOrEmpty(normalizedSearchTerm))
                {
                    query = query.Where(b => 
                        EF.Functions.Like(b.Title, "%" + normalizedSearchTerm + "%") ||
                        EF.Functions.Like(b.Isbn, "%" + normalizedSearchTerm + "%")
                    );
                }
            }

            if (searchRequest.CategoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == searchRequest.CategoryId.Value);
            }

            if (searchRequest.PublisherId.HasValue)
            {
                query = query.Where(b => b.PublisherId == searchRequest.PublisherId.Value);
            }

            // Note: Price filtering will be done after getting current prices
            // since current price comes from PriceChange table

            if (searchRequest.MinYear.HasValue)
            {
                query = query.Where(b => b.PublishYear >= searchRequest.MinYear.Value);
            }

            if (searchRequest.MaxYear.HasValue)
            {
                query = query.Where(b => b.PublishYear <= searchRequest.MaxYear.Value);
            }

            // Apply sorting
            query = searchRequest.SortBy?.ToLower() switch
            {
                "title" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.Title) 
                    : query.OrderBy(b => b.Title),
                "price" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.AveragePrice) 
                    : query.OrderBy(b => b.AveragePrice),
                "year" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.PublishYear) 
                    : query.OrderBy(b => b.PublishYear),
                "created" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.CreatedAt) 
                    : query.OrderBy(b => b.CreatedAt),
                _ => query.OrderBy(b => b.Title)
            };

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / searchRequest.PageSize);

            var booksData = await query
                .Skip((searchRequest.PageNumber - 1) * searchRequest.PageSize)
                .Take(searchRequest.PageSize)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList(),
                    Promotions = b.BookPromotions
                        .Where(bp => bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) 
                            && bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                        .Select(bp => new
                        {
                            bp.Promotion.PromotionId,
                            bp.Promotion.Name,
                            bp.Promotion.DiscountPct,
                            bp.Promotion.StartDate,
                            bp.Promotion.EndDate
                        }).ToList()
                })
                .ToListAsync();

            var books = new List<BookDto>();
            foreach (var bookData in booksData)
            {
                var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
                var discountedPrice = currentPrice;
                if (bookData.Promotions.Any())
                {
                    var maxDiscount = bookData.Promotions.Max(p => p.DiscountPct);
                    discountedPrice = currentPrice * (1 - maxDiscount / 100);
                }
                
                // Apply price filtering based on current price
                if (searchRequest.MinPrice.HasValue && currentPrice < searchRequest.MinPrice.Value)
                    continue;
                if (searchRequest.MaxPrice.HasValue && currentPrice > searchRequest.MaxPrice.Value)
                    continue;
                
                books.Add(new BookDto
                {
                    Isbn = bookData.Isbn,
                    Title = bookData.Title,
                    PageCount = bookData.PageCount,
                    AveragePrice = bookData.AveragePrice,
                    CurrentPrice = currentPrice,
                    DiscountedPrice = discountedPrice,
                    PublishYear = bookData.PublishYear,
                    CategoryId = bookData.CategoryId,
                    CategoryName = bookData.CategoryName,
                    PublisherId = bookData.PublisherId,
                    PublisherName = bookData.PublisherName,
                    ImageUrl = bookData.ImageUrl,
                    CreatedAt = bookData.CreatedAt,
                    UpdatedAt = bookData.UpdatedAt,
                    Stock = bookData.Stock,
                    Status = bookData.Status,
                    Authors = bookData.Authors,
                    HasPromotion = bookData.Promotions.Any(),
                    ActivePromotions = bookData.Promotions.Select(p => new BookPromotionDto
                    {
                        PromotionId = p.PromotionId,
                        Name = p.Name,
                        DiscountPct = p.DiscountPct,
                        StartDate = p.StartDate.ToDateTime(TimeOnly.MinValue),
                        EndDate = p.EndDate.ToDateTime(TimeOnly.MaxValue)
                    }).ToList()
                });
            }

            var response = new BookListResponse
            {
                Books = books,
                TotalCount = totalCount,
                PageNumber = searchRequest.PageNumber,
                PageSize = searchRequest.PageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<BookListResponse>
            {
                Success = true,
                Message = "Get books successfully",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookListResponse>
            {
                Success = false,
                Message = "Error getting books",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookListResponse>> SearchBooksAsync(BookSearchRequest searchRequest)
    {
        try
        {
            var query = _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Include(b => b.BookPromotions)
                    .ThenInclude(bp => bp.Promotion)
                .AsQueryable();

            // Apply search filters
            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                var normalizedSearchTerm = NormalizeSearchTerm(searchRequest.SearchTerm);
                if (!string.IsNullOrEmpty(normalizedSearchTerm))
                {
                    query = query.Where(b => 
                        EF.Functions.Like(b.Title, "%" + normalizedSearchTerm + "%") ||
                        EF.Functions.Like(b.Isbn, "%" + normalizedSearchTerm + "%") ||
                        b.AuthorBooks.Any(ab => 
                            EF.Functions.Like(ab.Author.FirstName, "%" + normalizedSearchTerm + "%") ||
                            EF.Functions.Like(ab.Author.LastName, "%" + normalizedSearchTerm + "%") ||
                            EF.Functions.Like(ab.Author.FirstName + " " + ab.Author.LastName, "%" + normalizedSearchTerm + "%")
                        ) ||
                        EF.Functions.Like(b.Category.Name, "%" + normalizedSearchTerm + "%") ||
                        EF.Functions.Like(b.Publisher.Name, "%" + normalizedSearchTerm + "%")
                    );
                }
            }

            if (searchRequest.CategoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == searchRequest.CategoryId.Value);
            }

            if (searchRequest.PublisherId.HasValue)
            {
                query = query.Where(b => b.PublisherId == searchRequest.PublisherId.Value);
            }

            if (searchRequest.MinYear.HasValue)
            {
                query = query.Where(b => b.PublishYear >= searchRequest.MinYear.Value);
            }

            if (searchRequest.MaxYear.HasValue)
            {
                query = query.Where(b => b.PublishYear <= searchRequest.MaxYear.Value);
            }

            // Apply sorting
            query = searchRequest.SortBy?.ToLower() switch
            {
                "title" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.Title) 
                    : query.OrderBy(b => b.Title),
                "price" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.AveragePrice) 
                    : query.OrderBy(b => b.AveragePrice),
                "year" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.PublishYear) 
                    : query.OrderBy(b => b.PublishYear),
                "created" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.CreatedAt) 
                    : query.OrderBy(b => b.CreatedAt),
                "popular" => searchRequest.SortDirection?.ToLower() == "desc" 
                    ? query.OrderByDescending(b => b.OrderLines.Sum(ol => ol.Qty)) 
                    : query.OrderBy(b => b.OrderLines.Sum(ol => ol.Qty)),
                _ => query.OrderBy(b => b.Title)
            };

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / searchRequest.PageSize);

            var booksData = await query
                .Skip((searchRequest.PageNumber - 1) * searchRequest.PageSize)
                .Take(searchRequest.PageSize)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList(),
                    Promotions = b.BookPromotions
                        .Where(bp => bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                        .Select(bp => new
                        {
                            bp.Promotion.PromotionId,
                            bp.Promotion.Name,
                            bp.Promotion.DiscountPct,
                            bp.Promotion.StartDate,
                            bp.Promotion.EndDate
                        }).ToList()
                })
                .ToListAsync();

            var books = new List<BookDto>();
            foreach (var bookData in booksData)
            {
                var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
                
                // Apply price filtering based on current price
                if (searchRequest.MinPrice.HasValue && currentPrice < searchRequest.MinPrice.Value)
                    continue;
                if (searchRequest.MaxPrice.HasValue && currentPrice > searchRequest.MaxPrice.Value)
                    continue;
                
                // Calculate discounted price if there are active promotions
                var discountedPrice = currentPrice;
                if (bookData.Promotions.Any())
                {
                    var maxDiscount = bookData.Promotions.Max(p => p.DiscountPct);
                    discountedPrice = currentPrice * (1 - maxDiscount / 100);
                }
                
                books.Add(new BookDto
                {
                    Isbn = bookData.Isbn,
                    Title = bookData.Title,
                    PageCount = bookData.PageCount,
                    AveragePrice = bookData.AveragePrice,
                    CurrentPrice = currentPrice,
                    PublishYear = bookData.PublishYear,
                    CategoryId = bookData.CategoryId,
                    CategoryName = bookData.CategoryName,
                    PublisherId = bookData.PublisherId,
                    PublisherName = bookData.PublisherName,
                    ImageUrl = bookData.ImageUrl,
                    CreatedAt = bookData.CreatedAt,
                    UpdatedAt = bookData.UpdatedAt,
                    Stock = bookData.Stock,
                    Status = bookData.Status,
                    Authors = bookData.Authors,
                    DiscountedPrice = discountedPrice,
                    HasPromotion = bookData.Promotions.Any(),
                    ActivePromotions = bookData.Promotions.Select(p => new BookPromotionDto
                    {
                        PromotionId = p.PromotionId,
                        Name = p.Name,
                        DiscountPct = p.DiscountPct,
                        StartDate = p.StartDate.ToDateTime(TimeOnly.MinValue),
                        EndDate = p.EndDate.ToDateTime(TimeOnly.MaxValue)
                    }).ToList()
                });
            }

            var response = new BookListResponse
            {
                Books = books,
                TotalCount = totalCount,
                PageNumber = searchRequest.PageNumber,
                PageSize = searchRequest.PageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<BookListResponse>
            {
                Success = true,
                Message = "Search books successfully",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookListResponse>
            {
                Success = false,
                Message = "Error searching books",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<BookDto>>> GetBooksWithPromotionAsync(int limit = 10)
    {
        try
        {
            var booksData = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Include(b => b.BookPromotions)
                    .ThenInclude(bp => bp.Promotion)
                .Where(b => b.Stock > 0 && b.Status == true && b.BookPromotions.Any(bp => 
                    bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) && 
                    bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow)))
                .OrderByDescending(b => b.BookPromotions.Max(bp => bp.Promotion.DiscountPct))
                .Take(limit)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList(),
                    Promotions = b.BookPromotions
                        .Where(bp => bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) 
                        && bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                        .Select(bp => new
                        {
                            bp.Promotion.PromotionId,
                            bp.Promotion.Name,
                            bp.Promotion.DiscountPct,
                            bp.Promotion.StartDate,
                            bp.Promotion.EndDate
                        }).ToList()
                })
                .ToListAsync();

            var books = new List<BookDto>();
            foreach (var bookData in booksData)
            {
                var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
                var maxDiscount = bookData.Promotions.Max(p => p.DiscountPct);
                var discountedPrice = currentPrice * (1 - maxDiscount / 100);
                
                books.Add(new BookDto
                {
                    Isbn = bookData.Isbn,
                    Title = bookData.Title,
                    PageCount = bookData.PageCount,
                    AveragePrice = bookData.AveragePrice,
                    CurrentPrice = currentPrice,
                    PublishYear = bookData.PublishYear,
                    CategoryId = bookData.CategoryId,
                    CategoryName = bookData.CategoryName,
                    PublisherId = bookData.PublisherId,
                    PublisherName = bookData.PublisherName,
                    ImageUrl = bookData.ImageUrl,
                    CreatedAt = bookData.CreatedAt,
                    UpdatedAt = bookData.UpdatedAt,
                    Stock = bookData.Stock,
                    Status = bookData.Status,
                    Authors = bookData.Authors,
                    DiscountedPrice = discountedPrice,
                    HasPromotion = true,
                    ActivePromotions = bookData.Promotions.Select(p => new BookPromotionDto
                    {
                        PromotionId = p.PromotionId,
                        Name = p.Name,
                        DiscountPct = p.DiscountPct,
                        StartDate = p.StartDate.ToDateTime(TimeOnly.MinValue),
                        EndDate = p.EndDate.ToDateTime(TimeOnly.MaxValue)
                    }).ToList()
                });
            }

            return new ApiResponse<List<BookDto>>
            {
                Success = true,
                Message = "Get books with promotion successfully",
                Data = books
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<BookDto>>
            {
                Success = false,
                Message = "Error getting books with promotion",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<BookDto>>> GetBestSellingBooksAsync(int limit = 10)
    {
        try
        {
            var booksData = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Include(b => b.BookPromotions)
                    .ThenInclude(bp => bp.Promotion)
                .Where(b => b.Stock > 0 && b.Status == true && b.OrderLines.Any())
                .OrderByDescending(b => b.OrderLines.Sum(ol => ol.Qty))
                .Take(limit)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    TotalSold = b.OrderLines.Sum(ol => ol.Qty),
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList(),
                    Promotions = b.BookPromotions
                        .Where(bp => bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) 
                        && bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                        .Select(bp => new
                        {
                            bp.Promotion.PromotionId,
                            bp.Promotion.Name,
                            bp.Promotion.DiscountPct,
                            bp.Promotion.StartDate,
                            bp.Promotion.EndDate
                        }).ToList()
                })
                .ToListAsync();

            var books = new List<BookDto>();
            foreach (var bookData in booksData)
            {
                var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
                var discountedPrice = currentPrice;
                if (bookData.Promotions.Any())
                {
                    var maxDiscount = bookData.Promotions.Max(p => p.DiscountPct);
                    discountedPrice = currentPrice * (1 - maxDiscount / 100);
                }
                
                books.Add(new BookDto
                {
                    Isbn = bookData.Isbn,
                    Title = bookData.Title,
                    PageCount = bookData.PageCount,
                    AveragePrice = bookData.AveragePrice,
                    CurrentPrice = currentPrice,
                    PublishYear = bookData.PublishYear,
                    CategoryId = bookData.CategoryId,
                    CategoryName = bookData.CategoryName,
                    PublisherId = bookData.PublisherId,
                    PublisherName = bookData.PublisherName,
                    ImageUrl = bookData.ImageUrl,
                    CreatedAt = bookData.CreatedAt,
                    UpdatedAt = bookData.UpdatedAt,
                    Stock = bookData.Stock,
                    Status = bookData.Status,
                    Authors = bookData.Authors,
                    DiscountedPrice = discountedPrice,
                    HasPromotion = bookData.Promotions.Any(),
                    TotalSold = bookData.TotalSold,
                    ActivePromotions = bookData.Promotions.Select(p => new BookPromotionDto
                    {
                        PromotionId = p.PromotionId,
                        Name = p.Name,
                        DiscountPct = p.DiscountPct,
                        StartDate = p.StartDate.ToDateTime(TimeOnly.MinValue),
                        EndDate = p.EndDate.ToDateTime(TimeOnly.MaxValue)
                    }).ToList()
                });
            }

            return new ApiResponse<List<BookDto>>
            {
                Success = true,
                Message = "Get best selling books successfully",
                Data = books
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<BookDto>>
            {
                Success = false,
                Message = "Error getting best selling books",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<BookDto>>> GetLatestBooksAsync(int limit = 10)
    {
        try
        {
            var booksData = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Include(b => b.BookPromotions)
                    .ThenInclude(bp => bp.Promotion)
                .Where(b => b.Stock > 0 && b.Status == true)
                .OrderByDescending(b => b.CreatedAt)
                .Take(limit)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList(),
                    Promotions = b.BookPromotions
                        .Where(bp => bp.Promotion.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) 
                        && bp.Promotion.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                        .Select(bp => new
                        {
                            bp.Promotion.PromotionId,
                            bp.Promotion.Name,
                            bp.Promotion.DiscountPct,
                            bp.Promotion.StartDate,
                            bp.Promotion.EndDate
                        }).ToList()
                })
                .ToListAsync();

            var books = new List<BookDto>();
            foreach (var bookData in booksData)
            {
                var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
                var discountedPrice = currentPrice;
                if (bookData.Promotions.Any())
                {
                    var maxDiscount = bookData.Promotions.Max(p => p.DiscountPct);
                    discountedPrice = currentPrice * (1 - maxDiscount / 100);
                }
                
                books.Add(new BookDto
                {
                    Isbn = bookData.Isbn,
                    Title = bookData.Title,
                    PageCount = bookData.PageCount,
                    AveragePrice = bookData.AveragePrice,
                    CurrentPrice = currentPrice,
                    PublishYear = bookData.PublishYear,
                    CategoryId = bookData.CategoryId,
                    CategoryName = bookData.CategoryName,
                    PublisherId = bookData.PublisherId,
                    PublisherName = bookData.PublisherName,
                    ImageUrl = bookData.ImageUrl,
                    CreatedAt = bookData.CreatedAt,
                    UpdatedAt = bookData.UpdatedAt,
                    Stock = bookData.Stock,
                    Status = bookData.Status,
                    Authors = bookData.Authors,
                    DiscountedPrice = discountedPrice,
                    HasPromotion = bookData.Promotions.Any(),
                    ActivePromotions = bookData.Promotions.Select(p => new BookPromotionDto
                    {
                        PromotionId = p.PromotionId,
                        Name = p.Name,
                        DiscountPct = p.DiscountPct,
                        StartDate = p.StartDate.ToDateTime(TimeOnly.MinValue),
                        EndDate = p.EndDate.ToDateTime(TimeOnly.MaxValue)
                    }).ToList()
                });
            }

            return new ApiResponse<List<BookDto>>
            {
                Success = true,
                Message = "Get latest books successfully",
                Data = books
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<BookDto>>
            {
                Success = false,
                Message = "Error getting latest books",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookDto>> GetBookByIsbnAsync(string isbn)
    {
        try
        {
            var bookData = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Where(b => b.Isbn == isbn)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (bookData == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Book not found",
                    Errors = new List<string> { "Book does not exist" }
                };
            }

            var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
            var book = new BookDto
            {
                Isbn = bookData.Isbn,
                Title = bookData.Title,
                PageCount = bookData.PageCount,
                AveragePrice = bookData.AveragePrice,
                CurrentPrice = currentPrice,
                PublishYear = bookData.PublishYear,
                CategoryId = bookData.CategoryId,
                CategoryName = bookData.CategoryName,
                PublisherId = bookData.PublisherId,
                PublisherName = bookData.PublisherName,
                ImageUrl = bookData.ImageUrl,
                CreatedAt = bookData.CreatedAt,
                UpdatedAt = bookData.UpdatedAt,
                Stock = bookData.Stock,
                Status = bookData.Status,
                Authors = bookData.Authors
            };

            return new ApiResponse<BookDto>
            {
                Success = true,
                Message = "Get book information successfully",
                Data = book
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookDto>
            {
                Success = false,
                Message = "Error getting book information",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookListResponse>> GetBooksByPublisherAsync(long publisherId, int pageNumber, int pageSize, string? searchTerm = null)
    {
        try
        {
            var query = _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Where(b => b.PublisherId == publisherId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(b => b.Title.Contains(searchTerm) ||
                                       b.Isbn.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var booksData = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList()
                })
                .ToListAsync();

            var books = new List<BookDto>();
            foreach (var bookData in booksData)
            {
                var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
                books.Add(new BookDto
                {
                    Isbn = bookData.Isbn,
                    Title = bookData.Title,
                    PageCount = bookData.PageCount,
                    AveragePrice = bookData.AveragePrice,
                    CurrentPrice = currentPrice,
                    PublishYear = bookData.PublishYear,
                    CategoryId = bookData.CategoryId,
                    CategoryName = bookData.CategoryName,
                    PublisherId = bookData.PublisherId,
                    PublisherName = bookData.PublisherName,
                    ImageUrl = bookData.ImageUrl,
                    CreatedAt = bookData.CreatedAt,
                    UpdatedAt = bookData.UpdatedAt,
                    Stock = bookData.Stock,
                    Status = bookData.Status,
                    Authors = bookData.Authors
                });
            }

            var response = new BookListResponse
            {
                Books = books,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<BookListResponse>
            {
                Success = true,
                Message = "Get books by publisher successfully",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookListResponse>
            {
                Success = false,
                Message = "Error getting books by publisher",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookListResponse>> GetBooksByCategoryAsync(long categoryId, int pageNumber, int pageSize, string? searchTerm = null)
    {
        try
        {
            var query = _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Where(b => b.CategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(b => b.Title.Contains(searchTerm) ||
                                       b.Isbn.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var booksData = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList()
                })
                .ToListAsync();

            var books = new List<BookDto>();
            foreach (var bookData in booksData)
            {
                var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
                books.Add(new BookDto
                {
                    Isbn = bookData.Isbn,
                    Title = bookData.Title,
                    PageCount = bookData.PageCount,
                    AveragePrice = bookData.AveragePrice,
                    CurrentPrice = currentPrice,
                    PublishYear = bookData.PublishYear,
                    CategoryId = bookData.CategoryId,
                    CategoryName = bookData.CategoryName,
                    PublisherId = bookData.PublisherId,
                    PublisherName = bookData.PublisherName,
                    ImageUrl = bookData.ImageUrl,
                    CreatedAt = bookData.CreatedAt,
                    UpdatedAt = bookData.UpdatedAt,
                    Stock = bookData.Stock,
                    Status = bookData.Status,
                    Authors = bookData.Authors
                });
            }

            var response = new BookListResponse
            {
                Books = books,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<BookListResponse>
            {
                Success = true,
                Message = "Get books by category successfully",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookListResponse>
            {
                Success = false,
                Message = "Error getting books by category",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookDto>> CreateBookAsync(CreateBookDto createBookDto, long? employeeId = null)
    {
        try
        {
            // Check if ISBN already exists
            var existingBook = await _context.Books
                .FirstOrDefaultAsync(b => b.Isbn == createBookDto.Isbn);

            if (existingBook != null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "ISBN already exists",
                    Errors = new List<string> { "Book with this ISBN already exists" }
                };
            }

            // Validate category exists
            var category = await _context.Categories.FindAsync(createBookDto.CategoryId);
            if (category == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Category not found",
                    Errors = new List<string> { "Selected category does not exist" }
                };
            }

            // Validate publisher exists
            var publisher = await _context.Publishers.FindAsync(createBookDto.PublisherId);
            if (publisher == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Publisher not found",
                    Errors = new List<string> { "Selected publisher does not exist" }
                };
            }

            // Validate authors exist
            var authorIds = createBookDto.AuthorIds.Distinct().ToList();
            var existingAuthors = await _context.Authors
                .Where(a => authorIds.Contains(a.AuthorId))
                .Select(a => a.AuthorId)
                .ToListAsync();

            var missingAuthorIds = authorIds.Except(existingAuthors).ToList();
            if (missingAuthorIds.Any())
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Authors not found",
                    Errors = new List<string> { $"Authors with IDs {string.Join(", ", missingAuthorIds)} do not exist" }
                };
            }

            // Handle image upload
            string? imageUrl = null;
            if (createBookDto.ImageFile != null && createBookDto.ImageFile.Length > 0)
            {
                imageUrl = await UploadImageToCloudinaryAsync(createBookDto.ImageFile, createBookDto.Isbn);
                if (string.IsNullOrEmpty(imageUrl))
                {
                    return new ApiResponse<BookDto>
                    {
                        Success = false,
                        Message = "Cannot upload image to Cloudinary",
                        Errors = new List<string> { "Error occurred while uploading image" }
                    };
                }
            }

            var book = new Book
            {
                Isbn = createBookDto.Isbn,
                Title = createBookDto.Title,
                PageCount = createBookDto.PageCount,
                AveragePrice = createBookDto.InitialPrice,
                PublishYear = createBookDto.PublishYear,
                CategoryId = createBookDto.CategoryId,
                PublisherId = createBookDto.PublisherId,
                ImageUrl = imageUrl
            };

            _context.Books.Add(book);

            // Create initial price change record
            var initialPriceChange = new PriceChange
            {
                Isbn = createBookDto.Isbn,
                OldPrice = 0,
                NewPrice = createBookDto.InitialPrice,
                ChangedAt = DateTime.UtcNow,
                EmployeeId = employeeId ?? 1 // Use provided employeeId or default to 1 (admin)
            };
            _context.PriceChanges.Add(initialPriceChange);

            // Add author-book relationships
            foreach (var authorId in authorIds)
            {
                _context.AuthorBooks.Add(new AuthorBook
                {
                    AuthorId = authorId,
                    Isbn = book.Isbn
                });
            }

            await _context.SaveChangesAsync();

            // Load the created book with related data
            var createdBookData = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Where(b => b.Isbn == book.Isbn)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList()
                })
                .FirstAsync();

            var currentPrice = await GetCurrentPriceAsync(createdBookData.Isbn);
            var createdBook = new BookDto
            {
                Isbn = createdBookData.Isbn,
                Title = createdBookData.Title,
                PageCount = createdBookData.PageCount,
                AveragePrice = createdBookData.AveragePrice,
                CurrentPrice = currentPrice,
                PublishYear = createdBookData.PublishYear,
                CategoryId = createdBookData.CategoryId,
                CategoryName = createdBookData.CategoryName,
                PublisherId = createdBookData.PublisherId,
                PublisherName = createdBookData.PublisherName,
                ImageUrl = createdBookData.ImageUrl,
                CreatedAt = createdBookData.CreatedAt,
                UpdatedAt = createdBookData.UpdatedAt,
                Stock = createdBookData.Stock,
                Status = createdBookData.Status,
                Authors = createdBookData.Authors
            };

            return new ApiResponse<BookDto>
            {
                Success = true,
                Message = "Book created successfully",
                Data = createdBook
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookDto>
            {
                Success = false,
                Message = "Error creating book",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookDto>> UpdateBookAsync(string isbn, UpdateBookDto updateBookDto)
    {
        try
        {
            var book = await _context.Books.FindAsync(isbn);
            if (book == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Book not found",
                    Errors = new List<string> { "Book does not exist" }
                };
            }

            // Validate category exists
            var category = await _context.Categories.FindAsync(updateBookDto.CategoryId);
            if (category == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Category not found",
                    Errors = new List<string> { "Selected category does not exist" }
                };
            }

            // Validate publisher exists
            var publisher = await _context.Publishers.FindAsync(updateBookDto.PublisherId);
            if (publisher == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Publisher not found",
                    Errors = new List<string> { "Selected publisher does not exist" }
                };
            }

            // Validate authors exist
            var authorIds = updateBookDto.AuthorIds.Distinct().ToList();
            var existingAuthors = await _context.Authors
                .Where(a => authorIds.Contains(a.AuthorId))
                .Select(a => a.AuthorId)
                .ToListAsync();

            var missingAuthorIds = authorIds.Except(existingAuthors).ToList();
            if (missingAuthorIds.Any())
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Authors not found",
                    Errors = new List<string> { $"Authors with IDs {string.Join(", ", missingAuthorIds)} do not exist" }
                };
            }

            // Handle image upload - only update if new image is provided
            string? finalImageUrl = book.ImageUrl; // Keep existing image by default
            if (updateBookDto.ImageFile != null && updateBookDto.ImageFile.Length > 0)
            {
                var uploadedImageUrl = await UploadImageToCloudinaryAsync(updateBookDto.ImageFile, isbn);
                if (!string.IsNullOrEmpty(uploadedImageUrl))
                {
                    finalImageUrl = uploadedImageUrl;
                }
                else
                {
                    return new ApiResponse<BookDto>
                    {
                        Success = false,
                        Message = "Cannot upload image to Cloudinary",
                        Errors = new List<string> { "Error occurred while uploading image" }
                    };
                }
            }
            else if (!string.IsNullOrEmpty(updateBookDto.ImageUrl))
            {
                // If no file upload but ImageUrl is provided, use the provided URL
                finalImageUrl = updateBookDto.ImageUrl;
            }

            // Update book properties - only update if provided
            book.Title = updateBookDto.Title;
            book.PageCount = updateBookDto.PageCount;
            // Note: Price updates should be done through PriceChange table, not directly on Book
            // book.AveragePrice will be updated by trigger when PriceChange is added
            book.PublishYear = updateBookDto.PublishYear;
            book.CategoryId = updateBookDto.CategoryId;
            book.PublisherId = updateBookDto.PublisherId;
            
            // Only update Stock if provided
            if (updateBookDto.Stock.HasValue)
            {
                book.Stock = updateBookDto.Stock.Value;
            }
            
            // Only update Status if provided
            if (updateBookDto.Status.HasValue)
            {
                book.Status = updateBookDto.Status.Value;
            }
            
            book.ImageUrl = finalImageUrl;
            book.UpdatedAt = DateTime.UtcNow;

            // Update author-book relationships
            var existingAuthorBooks = await _context.AuthorBooks
                .Where(ab => ab.Isbn == isbn)
                .ToListAsync();

            _context.AuthorBooks.RemoveRange(existingAuthorBooks);

            foreach (var authorId in authorIds)
            {
                _context.AuthorBooks.Add(new AuthorBook
                {
                    AuthorId = authorId,
                    Isbn = isbn
                });
            }

            await _context.SaveChangesAsync();

            // Return the updated book
            return await GetBookByIsbnAsync(isbn);
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookDto>
            {
                Success = false,
                Message = "Error updating book",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> DeleteBookAsync(string isbn)
    {
        try
        {
            var book = await _context.Books.FindAsync(isbn);
            if (book == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Book not found",
                    Errors = new List<string> { "Book does not exist" }
                };
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Book deleted successfully",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Error deleting book",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> DeactivateBookAsync(string isbn)
    {
        try
        {
            var book = await _context.Books.FindAsync(isbn);
            if (book == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Book not found",
                    Errors = new List<string> { "Book does not exist" }
                };
            }

            book.Status = false;
            book.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Book deactivated successfully",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Error deactivating book",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> ActivateBookAsync(string isbn)
    {
        try
        {
            var book = await _context.Books.FindAsync(isbn);
            if (book == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Book not found",
                    Errors = new List<string> { "Book does not exist" }
                };
            }

            book.Status = true;
            book.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Book activated successfully",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Error activating book",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<AuthorDto>>> GetAuthorsAsync()
    {
        try
        {
            var authors = await _context.Authors
                .OrderBy(a => a.FirstName)
                .ThenBy(a => a.LastName)
                .Select(a => new AuthorDto
                {
                    AuthorId = a.AuthorId,
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    FullName = a.FirstName + " " + a.LastName,
                    Gender = a.Gender,
                    DateOfBirth = a.DateOfBirth
                })
                .ToListAsync();

            return new ApiResponse<List<AuthorDto>>
            {
                Success = true,
                Message = "Get authors successfully",
                Data = authors
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<AuthorDto>>
            {
                Success = false,
                Message = "Error getting authors",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<AuthorDto>> CreateAuthorAsync(CreateAuthorDto createAuthorDto)
    {
        try
        {
            var author = new Author
            {
                FirstName = createAuthorDto.FirstName,
                LastName = createAuthorDto.LastName,
                Gender = createAuthorDto.Gender,
                DateOfBirth = createAuthorDto.DateOfBirth
            };

            _context.Authors.Add(author);
            await _context.SaveChangesAsync();

            var authorDto = new AuthorDto
            {
                AuthorId = author.AuthorId,
                FirstName = author.FirstName,
                LastName = author.LastName,
                FullName = author.FirstName + " " + author.LastName,
                Gender = author.Gender,
                DateOfBirth = author.DateOfBirth
            };

            return new ApiResponse<AuthorDto>
            {
                Success = true,
                Message = "Author created successfully",
                Data = authorDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthorDto>
            {
                Success = false,
                Message = "Error creating author",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookListResponse>> GetNewestBooksAsync(int limit = 10)
    {
        try
        {
            var booksData = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .OrderByDescending(b => b.CreatedAt)
                .Take(limit)
                .Select(b => new
                {
                    b.Isbn,
                    b.Title,
                    b.PageCount,
                    b.AveragePrice,
                    b.PublishYear,
                    b.CategoryId,
                    CategoryName = b.Category.Name,
                    b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    b.ImageUrl,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.Stock,
                    b.Status,
                    Authors = b.AuthorBooks.Select(ab => new AuthorDto
                    {
                        AuthorId = ab.Author.AuthorId,
                        FirstName = ab.Author.FirstName,
                        LastName = ab.Author.LastName,
                        FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                        Gender = ab.Author.Gender,
                        DateOfBirth = ab.Author.DateOfBirth
                    }).ToList()
                })
                .ToListAsync();

            var books = new List<BookDto>();
            foreach (var bookData in booksData)
            {
                var currentPrice = await GetCurrentPriceAsync(bookData.Isbn);
                books.Add(new BookDto
                {
                    Isbn = bookData.Isbn,
                    Title = bookData.Title,
                    PageCount = bookData.PageCount,
                    AveragePrice = bookData.AveragePrice,
                    CurrentPrice = currentPrice,
                    PublishYear = bookData.PublishYear,
                    CategoryId = bookData.CategoryId,
                    CategoryName = bookData.CategoryName,
                    PublisherId = bookData.PublisherId,
                    PublisherName = bookData.PublisherName,
                    ImageUrl = bookData.ImageUrl,
                    CreatedAt = bookData.CreatedAt,
                    UpdatedAt = bookData.UpdatedAt,
                    Stock = bookData.Stock,
                    Status = bookData.Status,
                    Authors = bookData.Authors
                });
            }

            var response = new BookListResponse
            {
                Books = books,
                TotalCount = books.Count,
                PageNumber = 1,
                PageSize = books.Count,
                TotalPages = 1
            };

            return new ApiResponse<BookListResponse>
            {
                Success = true,
                Message = "Get newest books successfully",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookListResponse>
            {
                Success = false,
                Message = "Error getting newest books",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<string?> UploadImageToCloudinaryAsync(IFormFile imageFile, string isbn)
    {
        try
        {
            var cloudinarySettings = _configuration.GetSection("Cloudinary");
            var cloudName = cloudinarySettings["CloudName"];
            var apiKey = cloudinarySettings["ApiKey"];
            var apiSecret = cloudinarySettings["ApiSecret"];

            if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                return null;
            }

            var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
            var cloudinary = new Cloudinary(account);

            using var stream = imageFile.OpenReadStream();
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(imageFile.FileName, stream),
                PublicId = $"bookstore/books/{isbn}",
                Overwrite = true
            };

            var uploadResult = await cloudinary.UploadAsync(uploadParams);
            return uploadResult.SecureUrl?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
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

