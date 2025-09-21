using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IBookService
{
    Task<ApiResponse<BookListResponse>> GetBooksAsync(BookSearchRequest searchRequest);
    Task<ApiResponse<BookDto>> GetBookByIsbnAsync(string isbn);
    Task<ApiResponse<BookListResponse>> GetBooksByPublisherAsync(long publisherId, int pageNumber, int pageSize, string? searchTerm = null);
    Task<ApiResponse<BookDto>> CreateBookAsync(CreateBookDto createBookDto);
    Task<ApiResponse<BookDto>> UpdateBookAsync(string isbn, UpdateBookDto updateBookDto);
    Task<ApiResponse<bool>> DeleteBookAsync(string isbn);
    Task<ApiResponse<List<AuthorDto>>> GetAuthorsAsync();
    Task<ApiResponse<AuthorDto>> CreateAuthorAsync(CreateAuthorDto createAuthorDto);
}
