using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BookStore.Api.Data;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookController : ControllerBase
{
    private readonly IBookService _bookService;
    private readonly BookStoreDbContext _context;
    private readonly ILogger<BookController> _logger;

    public BookController(IBookService bookService, BookStoreDbContext context, ILogger<BookController> logger)
    {
        _bookService = bookService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách sách với tìm kiếm và phân trang
    /// </summary>
    /// <param name="searchRequest">Tham số tìm kiếm</param>
    /// <returns>Danh sách sách</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BookListResponse>>> GetBooks([FromQuery] BookSearchRequest searchRequest)
    {
        var result = await _bookService.GetBooksAsync(searchRequest);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Lấy thông tin sách theo ISBN
    /// </summary>
    /// <param name="isbn">ISBN của sách</param>
    /// <returns>Thông tin sách</returns>
    [HttpGet("{isbn}")]
    public async Task<ActionResult<ApiResponse<BookDto>>> GetBook(string isbn)
    {
        var result = await _bookService.GetBookByIsbnAsync(isbn);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }

    /// <summary>
    /// Lấy sách theo danh mục
    /// </summary>
    /// <param name="categoryId">ID danh mục</param>
    /// <param name="pageNumber">Trang (mặc định 1)</param>
    /// <param name="pageSize">Kích thước trang (mặc định 12)</param>
    /// <param name="search">Từ khóa tìm kiếm tuỳ chọn</param>
    [HttpGet("categories/{categoryId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BookListResponse>>> GetBooksByCategory(long categoryId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 12, [FromQuery] string? search = null)
    {
        var result = await _bookService.GetBooksByCategoryAsync(categoryId, pageNumber, pageSize, search);
        if (result.Success)
        {
            return Ok(result);
        }
        return BadRequest(result);
    }

    /// <summary>
    /// Lấy danh sách sách theo nhà xuất bản
    /// </summary>
    /// <param name="publisherId">ID nhà xuất bản</param>
    /// <param name="pageNumber">Số trang (mặc định: 1)</param>
    /// <param name="pageSize">Kích thước trang (mặc định: 10)</param>
    /// <param name="searchTerm">Từ khóa tìm kiếm</param>
    /// <returns>Danh sách sách theo nhà xuất bản</returns>
    [HttpGet("by-publisher/{publisherId}")]
    public async Task<ActionResult<ApiResponse<BookListResponse>>> GetBooksByPublisher(
        long publisherId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        var result = await _bookService.GetBooksByPublisherAsync(publisherId, pageNumber, pageSize, searchTerm);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Tạo sách mới với upload ảnh
    /// </summary>
    /// <param name="isbn">ISBN của sách</param>
    /// <param name="title">Tên sách</param>
    /// <param name="categoryId">ID danh mục</param>
    /// <param name="publisherId">ID nhà xuất bản</param>
    /// <param name="unitPrice">Giá bán</param>
    /// <param name="publishYear">Năm xuất bản</param>
    /// <param name="pageCount">Số trang</param>
    /// <param name="stock">Số lượng tồn kho</param>
    /// <param name="authorIds">Danh sách ID tác giả (comma-separated)</param>
    /// <param name="imageFile">File ảnh</param>
    /// <returns>Thông tin sách đã tạo</returns>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<BookDto>>> CreateBook(
        [FromForm] string isbn,
        [FromForm] string title,
        [FromForm] long categoryId,
        [FromForm] long publisherId,
        [FromForm] decimal unitPrice,
        [FromForm] int publishYear,
        [FromForm] int pageCount,
        [FromForm] int stock,
        [FromForm] string authorIds,
        [FromForm] IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<BookDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        // Parse authorIds from comma-separated string
        var authorIdList = new List<long>();
        if (!string.IsNullOrWhiteSpace(authorIds))
        {
            authorIdList = authorIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => long.TryParse(id.Trim(), out var parsedId) ? parsedId : 0)
                .Where(id => id > 0)
                .ToList();
        }

        // Create CreateBookDto from form parameters
        var createBookDto = new CreateBookDto
        {
            Isbn = isbn,
            Title = title,
            CategoryId = categoryId,
            PublisherId = publisherId,
            InitialPrice = unitPrice,
            PublishYear = publishYear,
            PageCount = pageCount,
            AuthorIds = authorIdList,
            ImageFile = imageFile
        };

        var result = await _bookService.CreateBookAsync(createBookDto, await GetEmployeeIdFromToken());
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetBook), new { isbn = result.Data!.Isbn }, result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Cập nhật sách
    /// </summary>
    /// <param name="isbn">ISBN của sách</param>
    /// <param name="title">Tên sách</param>
    /// <param name="categoryId">ID danh mục</param>
    /// <param name="publisherId">ID nhà xuất bản</param>
    /// <param name="unitPrice">Giá bán</param>
    /// <param name="publishYear">Năm xuất bản</param>
    /// <param name="pageCount">Số trang</param>
    /// <param name="stock">Số lượng tồn kho</param>
    /// <param name="status">Trạng thái sách</param>
    /// <param name="authorIds">Danh sách ID tác giả (comma-separated)</param>
    /// <param name="imageUrl">URL ảnh hiện tại (nếu không upload ảnh mới)</param>
    /// <param name="imageFile">File ảnh mới (nếu có)</param>
    /// <returns>Thông tin sách đã cập nhật</returns>
    [HttpPut("{isbn}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<BookDto>>> UpdateBook(
        string isbn,
        [FromForm] string title,
        [FromForm] long categoryId,
        [FromForm] long publisherId,
        [FromForm] decimal unitPrice,
        [FromForm] int publishYear,
        [FromForm] int pageCount,
        [FromForm] int? stock = null,
        [FromForm] bool? status = null,
        [FromForm] string authorIds = "",
        [FromForm] string? imageUrl = null,
        [FromForm] IFormFile? imageFile = null)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<BookDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        // Parse authorIds from comma-separated string
        var authorIdList = new List<long>();
        if (!string.IsNullOrWhiteSpace(authorIds))
        {
            authorIdList = authorIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => long.TryParse(id.Trim(), out var parsedId) ? parsedId : 0)
                .Where(id => id > 0)
                .ToList();
        }

        // Create UpdateBookDto from form parameters
        var updateBookDto = new UpdateBookDto
        {
            Title = title,
            CategoryId = categoryId,
            PublisherId = publisherId,
            InitialPrice = unitPrice,
            PublishYear = publishYear,
            PageCount = pageCount,
            Stock = stock,
            Status = status,
            AuthorIds = authorIdList,
            ImageUrl = imageUrl,
            ImageFile = imageFile
        };

        var result = await _bookService.UpdateBookAsync(isbn, updateBookDto);
        
        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message.Contains("Không tìm thấy"))
        {
            return NotFound(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Xóa sách
    /// </summary>
    /// <param name="isbn">ISBN của sách</param>
    /// <returns>Kết quả xóa</returns>
    [HttpDelete("{isbn}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteBook(string isbn)
    {
        var result = await _bookService.DeleteBookAsync(isbn);
        
        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message.Contains("Không tìm thấy"))
        {
            return NotFound(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Lấy danh sách tác giả
    /// </summary>
    /// <returns>Danh sách tác giả</returns>
    [HttpGet("authors")]
    public async Task<ActionResult<ApiResponse<List<AuthorDto>>>> GetAuthors()
    {
        var result = await _bookService.GetAuthorsAsync();
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Tạo tác giả mới
    /// </summary>
    /// <param name="createAuthorDto">Thông tin tác giả mới</param>
    /// <returns>Thông tin tác giả đã tạo</returns>
    [HttpPost("authors")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<AuthorDto>>> CreateAuthor([FromBody] CreateAuthorDto createAuthorDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<AuthorDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _bookService.CreateAuthorAsync(createAuthorDto);
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetAuthors), new { }, result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Search books with advanced filtering and sorting
    /// </summary>
    /// <param name="searchRequest">Search criteria</param>
    /// <returns>List of books matching search criteria</returns>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BookListResponse>>> SearchBooks([FromQuery] BookSearchRequest searchRequest)
    {
        var result = await _bookService.SearchBooksAsync(searchRequest);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Get books with active promotions
    /// </summary>
    /// <param name="limit">Number of books to return (default: 10)</param>
    /// <returns>List of books with promotions</returns>
    [HttpGet("promotions")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<BookDto>>>> GetBooksWithPromotion([FromQuery] int limit = 10)
    {
        var result = await _bookService.GetBooksWithPromotionAsync(limit);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Get best selling books
    /// </summary>
    /// <param name="limit">Number of books to return (default: 10)</param>
    /// <returns>List of best selling books</returns>
    [HttpGet("bestsellers")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<BookDto>>>> GetBestSellingBooks([FromQuery] int limit = 10)
    {
        var result = await _bookService.GetBestSellingBooksAsync(limit);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Get latest books
    /// </summary>
    /// <param name="limit">Number of books to return (default: 10)</param>
    /// <returns>List of latest books</returns>
    [HttpGet("latest")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<BookDto>>>> GetLatestBooks([FromQuery] int limit = 10)
    {
        var result = await _bookService.GetLatestBooksAsync(limit);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Tắt (ngừng kinh doanh) sách - chỉ cập nhật trạng thái về 0
    /// </summary>
    /// <param name="isbn">Mã ISBN</param>
    [HttpPost("{isbn}/deactivate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> DeactivateBook(string isbn)
    {
        var result = await _bookService.DeactivateBookAsync(isbn);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Bật (kinh doanh lại) sách - cập nhật trạng thái về 1
    /// </summary>
    /// <param name="isbn">Mã ISBN</param>
    [HttpPost("{isbn}/activate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> ActivateBook(string isbn)
    {
        var result = await _bookService.ActivateBookAsync(isbn);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Test API hoạt động
    /// </summary>
    /// <returns>Thông báo API hoạt động</returns>
    [HttpGet("test")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<string>> TestApi()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "API hoạt động bình thường",
            Data = "Book API is running!"
        });
    }

    /// <summary>
    /// Test upload ảnh lên Cloudinary
    /// </summary>
    /// <param name="imageFile">File ảnh</param>
    /// <returns>URL ảnh từ Cloudinary</returns>
    [HttpPost("test-upload-image")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<string>>> TestUploadImage([FromForm] IFormFile imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            return BadRequest(new ApiResponse<string>
            {
                Success = false,
                Message = "Không có file ảnh được gửi",
                Errors = new List<string> { "File ảnh là bắt buộc" }
            });
        }

        try
        {
            var imageUrl = await _bookService.UploadImageToCloudinaryAsync(imageFile, "test");
            
            if (string.IsNullOrEmpty(imageUrl))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = "Không thể upload ảnh lên Cloudinary",
                    Errors = new List<string> { "Có lỗi xảy ra khi upload ảnh" }
                });
            }

            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = "Upload ảnh thành công",
                Data = imageUrl
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi upload ảnh",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    private async Task<long?> GetEmployeeIdFromToken()
    {
        try
        {
            // Lấy account ID từ token
            var nameIdentifierClaims = User.Claims.Where(c => c.Type == ClaimTypes.NameIdentifier).ToList();
            string? accountIdClaim = null;
            foreach (var claim in nameIdentifierClaims)
            {
                if (long.TryParse(claim.Value, out _))
                {
                    accountIdClaim = claim.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(accountIdClaim) || !long.TryParse(accountIdClaim, out long accountId))
            {
                return null;
            }

            // Tìm employee ID từ account ID
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.AccountId == accountId);
            
            return employee?.EmployeeId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee ID from token");
            return null;
        }
    }
}
