using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class BookService : IBookService
{
    private readonly BookStoreDbContext _context;

    public BookService(BookStoreDbContext context)
    {
        _context = context;
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
                .AsQueryable();

            // Apply search filters
            if (!string.IsNullOrWhiteSpace(searchRequest.SearchTerm))
            {
                query = query.Where(b => b.Title.Contains(searchRequest.SearchTerm) ||
                                       b.Isbn.Contains(searchRequest.SearchTerm));
            }

            if (searchRequest.CategoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == searchRequest.CategoryId.Value);
            }

            if (searchRequest.PublisherId.HasValue)
            {
                query = query.Where(b => b.PublisherId == searchRequest.PublisherId.Value);
            }

            if (searchRequest.MinPrice.HasValue)
            {
                query = query.Where(b => b.UnitPrice >= searchRequest.MinPrice.Value);
            }

            if (searchRequest.MaxPrice.HasValue)
            {
                query = query.Where(b => b.UnitPrice <= searchRequest.MaxPrice.Value);
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
                    ? query.OrderByDescending(b => b.UnitPrice) 
                    : query.OrderBy(b => b.UnitPrice),
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

            var books = await query
                .Skip((searchRequest.PageNumber - 1) * searchRequest.PageSize)
                .Take(searchRequest.PageSize)
                .Select(b => new BookDto
                {
                    Isbn = b.Isbn,
                    Title = b.Title,
                    PageCount = b.PageCount,
                    UnitPrice = b.UnitPrice,
                    PublishYear = b.PublishYear,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category.Name,
                    PublisherId = b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    ImageUrl = b.ImageUrl,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt,
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
                Message = "Lấy danh sách sách thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookListResponse>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy danh sách sách",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookDto>> GetBookByIsbnAsync(string isbn)
    {
        try
        {
            var book = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Where(b => b.Isbn == isbn)
                .Select(b => new BookDto
                {
                    Isbn = b.Isbn,
                    Title = b.Title,
                    PageCount = b.PageCount,
                    UnitPrice = b.UnitPrice,
                    PublishYear = b.PublishYear,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category.Name,
                    PublisherId = b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    ImageUrl = b.ImageUrl,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt,
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

            if (book == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Không tìm thấy sách",
                    Errors = new List<string> { "Sách không tồn tại" }
                };
            }

            return new ApiResponse<BookDto>
            {
                Success = true,
                Message = "Lấy thông tin sách thành công",
                Data = book
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy thông tin sách",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookListResponse>> GetBooksByPublisherAsync(long publisherId, int pageNumber, int pageSize, string? searchTerm = null)
    {
        try
        {
            // Check if publisher exists
            var publisherExists = await _context.Publishers.AnyAsync(p => p.PublisherId == publisherId);
            if (!publisherExists)
            {
                return new ApiResponse<BookListResponse>
                {
                    Success = false,
                    Message = "Không tìm thấy nhà xuất bản",
                    Errors = new List<string> { "Nhà xuất bản không tồn tại" }
                };
            }

            var query = _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Where(b => b.PublisherId == publisherId);

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(b => b.Title.Contains(searchTerm) || 
                                       b.Category.Name.Contains(searchTerm) ||
                                       b.AuthorBooks.Any(ab => ab.Author.FirstName.Contains(searchTerm) || 
                                                              ab.Author.LastName.Contains(searchTerm)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var books = await query
                .OrderBy(b => b.Title)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BookDto
                {
                    Isbn = b.Isbn,
                    Title = b.Title,
                    PageCount = b.PageCount,
                    UnitPrice = b.UnitPrice,
                    PublishYear = b.PublishYear,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category.Name,
                    PublisherId = b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    ImageUrl = b.ImageUrl,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt,
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
                Message = "Lấy danh sách sách theo nhà xuất bản thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookListResponse>
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy danh sách sách theo nhà xuất bản",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookDto>> CreateBookAsync(CreateBookDto createBookDto)
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
                    Message = "ISBN đã tồn tại",
                    Errors = new List<string> { "Sách với ISBN này đã được tạo trước đó" }
                };
            }

            // Validate category exists
            var category = await _context.Categories.FindAsync(createBookDto.CategoryId);
            if (category == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Danh mục không tồn tại",
                    Errors = new List<string> { "Danh mục được chọn không tồn tại" }
                };
            }

            // Validate publisher exists
            var publisher = await _context.Publishers.FindAsync(createBookDto.PublisherId);
            if (publisher == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Nhà xuất bản không tồn tại",
                    Errors = new List<string> { "Nhà xuất bản được chọn không tồn tại" }
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
                    Message = "Tác giả không tồn tại",
                    Errors = new List<string> { $"Các tác giả với ID {string.Join(", ", missingAuthorIds)} không tồn tại" }
                };
            }

            var book = new Book
            {
                Isbn = createBookDto.Isbn,
                Title = createBookDto.Title,
                PageCount = createBookDto.PageCount,
                UnitPrice = createBookDto.UnitPrice,
                PublishYear = createBookDto.PublishYear,
                CategoryId = createBookDto.CategoryId,
                PublisherId = createBookDto.PublisherId,
                ImageUrl = createBookDto.ImageUrl
            };

            _context.Books.Add(book);

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
            var createdBook = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Where(b => b.Isbn == book.Isbn)
                .Select(b => new BookDto
                {
                    Isbn = b.Isbn,
                    Title = b.Title,
                    PageCount = b.PageCount,
                    UnitPrice = b.UnitPrice,
                    PublishYear = b.PublishYear,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category.Name,
                    PublisherId = b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    ImageUrl = b.ImageUrl,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt,
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

            return new ApiResponse<BookDto>
            {
                Success = true,
                Message = "Tạo sách thành công",
                Data = createdBook
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi tạo sách",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<BookDto>> UpdateBookAsync(string isbn, UpdateBookDto updateBookDto)
    {
        try
        {
            var book = await _context.Books
                .Include(b => b.AuthorBooks)
                .FirstOrDefaultAsync(b => b.Isbn == isbn);

            if (book == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Không tìm thấy sách",
                    Errors = new List<string> { "Sách không tồn tại" }
                };
            }

            // Validate category exists
            var category = await _context.Categories.FindAsync(updateBookDto.CategoryId);
            if (category == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Danh mục không tồn tại",
                    Errors = new List<string> { "Danh mục được chọn không tồn tại" }
                };
            }

            // Validate publisher exists
            var publisher = await _context.Publishers.FindAsync(updateBookDto.PublisherId);
            if (publisher == null)
            {
                return new ApiResponse<BookDto>
                {
                    Success = false,
                    Message = "Nhà xuất bản không tồn tại",
                    Errors = new List<string> { "Nhà xuất bản được chọn không tồn tại" }
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
                    Message = "Tác giả không tồn tại",
                    Errors = new List<string> { $"Các tác giả với ID {string.Join(", ", missingAuthorIds)} không tồn tại" }
                };
            }

            // Update book properties
            book.Title = updateBookDto.Title;
            book.PageCount = updateBookDto.PageCount;
            book.UnitPrice = updateBookDto.UnitPrice;
            book.PublishYear = updateBookDto.PublishYear;
            book.CategoryId = updateBookDto.CategoryId;
            book.PublisherId = updateBookDto.PublisherId;
            book.ImageUrl = updateBookDto.ImageUrl;
            book.UpdatedAt = DateTime.UtcNow;

            // Update author relationships
            var currentAuthorIds = book.AuthorBooks.Select(ab => ab.AuthorId).ToList();
            var authorsToAdd = authorIds.Except(currentAuthorIds).ToList();
            var authorsToRemove = currentAuthorIds.Except(authorIds).ToList();

            // Remove old relationships
            var authorBooksToRemove = book.AuthorBooks
                .Where(ab => authorsToRemove.Contains(ab.AuthorId))
                .ToList();
            foreach (var authorBook in authorBooksToRemove)
            {
                _context.AuthorBooks.Remove(authorBook);
            }

            // Add new relationships
            foreach (var authorId in authorsToAdd)
            {
                _context.AuthorBooks.Add(new AuthorBook
                {
                    AuthorId = authorId,
                    Isbn = book.Isbn
                });
            }

            await _context.SaveChangesAsync();

            // Load the updated book with related data
            var updatedBook = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .Where(b => b.Isbn == book.Isbn)
                .Select(b => new BookDto
                {
                    Isbn = b.Isbn,
                    Title = b.Title,
                    PageCount = b.PageCount,
                    UnitPrice = b.UnitPrice,
                    PublishYear = b.PublishYear,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category.Name,
                    PublisherId = b.PublisherId,
                    PublisherName = b.Publisher.Name,
                    ImageUrl = b.ImageUrl,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt,
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

            return new ApiResponse<BookDto>
            {
                Success = true,
                Message = "Cập nhật sách thành công",
                Data = updatedBook
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BookDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi cập nhật sách",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> DeleteBookAsync(string isbn)
    {
        try
        {
            var book = await _context.Books
                .Include(b => b.OrderLines)
                .Include(b => b.PurchaseOrderLines)
                .FirstOrDefaultAsync(b => b.Isbn == isbn);

            if (book == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy sách",
                    Errors = new List<string> { "Sách không tồn tại" }
                };
            }

            if (book.OrderLines.Any() || book.PurchaseOrderLines.Any())
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không thể xóa sách",
                    Errors = new List<string> { "Sách đang được sử dụng trong đơn hàng hoặc đơn đặt mua, không thể xóa" }
                };
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Xóa sách thành công",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi xóa sách",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<AuthorDto>>> GetAuthorsAsync()
    {
        try
        {
            var authors = await _context.Authors
                .Select(a => new AuthorDto
                {
                    AuthorId = a.AuthorId,
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    FullName = a.FirstName + " " + a.LastName,
                    Gender = a.Gender,
                    DateOfBirth = a.DateOfBirth,
                    Address = a.Address,
                    Email = a.Email,
                    BookCount = a.AuthorBooks.Count
                })
                .OrderBy(a => a.FullName)
                .ToListAsync();

            return new ApiResponse<List<AuthorDto>>
            {
                Success = true,
                Message = "Lấy danh sách tác giả thành công",
                Data = authors
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<AuthorDto>>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy danh sách tác giả",
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
                DateOfBirth = createAuthorDto.DateOfBirth,
                Address = createAuthorDto.Address,
                Email = createAuthorDto.Email
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
                DateOfBirth = author.DateOfBirth,
                Address = author.Address,
                Email = author.Email,
                BookCount = 0
            };

            return new ApiResponse<AuthorDto>
            {
                Success = true,
                Message = "Tạo tác giả thành công",
                Data = authorDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthorDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi tạo tác giả",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
