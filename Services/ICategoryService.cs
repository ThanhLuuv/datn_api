using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface ICategoryService
{
    Task<ApiResponse<CategoryListResponse>> GetCategoriesAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
    Task<ApiResponse<CategoryDto>> GetCategoryByIdAsync(long categoryId);
    Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto createCategoryDto);
    Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(long categoryId, UpdateCategoryDto updateCategoryDto);
    Task<ApiResponse<bool>> DeleteCategoryAsync(long categoryId);
}
