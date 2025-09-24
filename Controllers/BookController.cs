using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookController : ControllerBase
{
    private readonly IBookService _bookService;

    public BookController(IBookService bookService)
    {
        _bookService = bookService;
    }

    /// <summary>
    /// Lấy danh sách sách với tìm kiếm và phân trang
    /// </summary>
    /// <param name="searchRequest">Tham số tìm kiếm</param>
    /// <returns>Danh sách sách</returns>
    [HttpGet]
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
    /// Tạo sách mới
    /// </summary>
    /// <param name="createBookDto">Thông tin sách mới</param>
    /// <returns>Thông tin sách đã tạo</returns>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<BookDto>>> CreateBook([FromBody] CreateBookDto createBookDto)
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

        var result = await _bookService.CreateBookAsync(createBookDto);
        
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
    /// <param name="updateBookDto">Thông tin cập nhật</param>
    /// <returns>Thông tin sách đã cập nhật</returns>
    [HttpPut("{isbn}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<BookDto>>> UpdateBook(string isbn, [FromBody] UpdateBookDto updateBookDto)
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
}
