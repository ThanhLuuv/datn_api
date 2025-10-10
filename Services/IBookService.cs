using BookStore.Api.DTOs;
using Microsoft.AspNetCore.Http;

namespace BookStore.Api.Services;

public interface IBookService
{
	Task<ApiResponse<BookListResponse>> GetBooksAsync(BookSearchRequest searchRequest);
	Task<ApiResponse<BookListResponse>> SearchBooksAsync(BookSearchRequest searchRequest);
	Task<ApiResponse<BookDto>> GetBookByIsbnAsync(string isbn);
	Task<ApiResponse<BookListResponse>> GetBooksByPublisherAsync(long publisherId, int pageNumber, int pageSize, string? searchTerm = null);
	Task<ApiResponse<BookDto>> CreateBookAsync(CreateBookDto createBookDto);
	Task<ApiResponse<BookDto>> UpdateBookAsync(string isbn, UpdateBookDto updateBookDto);
	Task<ApiResponse<bool>> DeleteBookAsync(string isbn);
	Task<ApiResponse<bool>> DeactivateBookAsync(string isbn);
	Task<ApiResponse<bool>> ActivateBookAsync(string isbn);
	Task<ApiResponse<List<AuthorDto>>> GetAuthorsAsync();
	Task<ApiResponse<AuthorDto>> CreateAuthorAsync(CreateAuthorDto createAuthorDto);
    Task<ApiResponse<BookListResponse>> GetNewestBooksAsync(int limit = 10);
    Task<ApiResponse<List<BookDto>>> GetBooksWithPromotionAsync(int limit = 10);
    Task<ApiResponse<List<BookDto>>> GetBestSellingBooksAsync(int limit = 10);
    Task<ApiResponse<List<BookDto>>> GetLatestBooksAsync(int limit = 10);
    Task<string?> UploadImageToCloudinaryAsync(IFormFile imageFile, string isbn);
}
