using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class CategoryService : ICategoryService
{
    private readonly BookStoreDbContext _context;

    public CategoryService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<CategoryListResponse>> GetCategoriesAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
    {
        try
        {
            var query = _context.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(c => c.Name.Contains(searchTerm) || 
                                       (c.Description != null && c.Description.Contains(searchTerm)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var categories = await query
                .OrderBy(c => c.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description,
                    BookCount = c.Books.Count(b => b.Stock > 0 && b.Status == true)
                })
                .ToListAsync();

            var response = new CategoryListResponse
            {
                Categories = categories,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<CategoryListResponse>
            {
                Success = true,
                Message = "Lấy danh sách danh mục thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CategoryListResponse>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy danh sách danh mục",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<CategoryDto>> GetCategoryByIdAsync(long categoryId)
    {
        try
        {
            var category = await _context.Categories
                .Where(c => c.CategoryId == categoryId)
                .Select(c => new CategoryDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description,
                    BookCount = c.Books.Count(b => b.Stock > 0 && b.Status == true)
                })
                .FirstOrDefaultAsync();

            if (category == null)
            {
                return new ApiResponse<CategoryDto>
                {
                    Success = false,
                    Message = "Không tìm thấy danh mục",
                    Errors = new List<string> { "Danh mục không tồn tại" }
                };
            }

            return new ApiResponse<CategoryDto>
            {
                Success = true,
                Message = "Lấy thông tin danh mục thành công",
                Data = category
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CategoryDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi lấy thông tin danh mục",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto createCategoryDto)
    {
        try
        {
            // Check if category name already exists
            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name == createCategoryDto.Name);

            if (existingCategory != null)
            {
                return new ApiResponse<CategoryDto>
                {
                    Success = false,
                    Message = "Tên danh mục đã tồn tại",
                    Errors = new List<string> { "Danh mục với tên này đã được tạo trước đó" }
                };
            }

            var category = new Category
            {
                Name = createCategoryDto.Name,
                Description = createCategoryDto.Description
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var categoryDto = new CategoryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Description = category.Description,
                BookCount = 0
            };

            return new ApiResponse<CategoryDto>
            {
                Success = true,
                Message = "Tạo danh mục thành công",
                Data = categoryDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CategoryDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi tạo danh mục",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(long categoryId, UpdateCategoryDto updateCategoryDto)
    {
        try
        {
            var category = await _context.Categories.FindAsync(categoryId);

            if (category == null)
            {
                return new ApiResponse<CategoryDto>
                {
                    Success = false,
                    Message = "Không tìm thấy danh mục",
                    Errors = new List<string> { "Danh mục không tồn tại" }
                };
            }

            // Check if new name conflicts with existing category
            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name == updateCategoryDto.Name && c.CategoryId != categoryId);

            if (existingCategory != null)
            {
                return new ApiResponse<CategoryDto>
                {
                    Success = false,
                    Message = "Tên danh mục đã tồn tại",
                    Errors = new List<string> { "Danh mục với tên này đã được tạo trước đó" }
                };
            }

            category.Name = updateCategoryDto.Name;
            category.Description = updateCategoryDto.Description;

            await _context.SaveChangesAsync();

            var categoryDto = new CategoryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Description = category.Description,
                BookCount = category.Books.Count(b => b.Stock > 0 && b.Status == true)
            };

            return new ApiResponse<CategoryDto>
            {
                Success = true,
                Message = "Cập nhật danh mục thành công",
                Data = categoryDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CategoryDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi cập nhật danh mục",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> DeleteCategoryAsync(long categoryId)
    {
        try
        {
            var category = await _context.Categories
                .Include(c => c.Books)
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

            if (category == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy danh mục",
                    Errors = new List<string> { "Danh mục không tồn tại" }
                };
            }

            if (category.Books.Any())
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không thể xóa danh mục",
                    Errors = new List<string> { "Danh mục đang có sách, không thể xóa" }
                };
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Xóa danh mục thành công",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi xóa danh mục",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
