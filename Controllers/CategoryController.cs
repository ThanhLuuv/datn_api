using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Lấy danh sách danh mục
    /// </summary>
    /// <param name="pageNumber">Số trang (mặc định: 1)</param>
    /// <param name="pageSize">Kích thước trang (mặc định: 10)</param>
    /// <param name="searchTerm">Từ khóa tìm kiếm</param>
    /// <returns>Danh sách danh mục</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CategoryListResponse>>> GetCategories(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var result = await _categoryService.GetCategoriesAsync(pageNumber, pageSize, searchTerm);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Lấy thông tin danh mục theo ID
    /// </summary>
    /// <param name="categoryId">ID danh mục</param>
    /// <returns>Thông tin danh mục</returns>
    [HttpGet("{categoryId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> GetCategory(long categoryId)
    {
        var result = await _categoryService.GetCategoryByIdAsync(categoryId);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }

    /// <summary>
    /// Tạo danh mục mới
    /// </summary>
    /// <param name="createCategoryDto">Thông tin danh mục mới</param>
    /// <returns>Thông tin danh mục đã tạo</returns>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> CreateCategory([FromBody] CreateCategoryDto createCategoryDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<CategoryDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _categoryService.CreateCategoryAsync(createCategoryDto);
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetCategory), new { categoryId = result.Data!.CategoryId }, result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Cập nhật danh mục
    /// </summary>
    /// <param name="categoryId">ID danh mục</param>
    /// <param name="updateCategoryDto">Thông tin cập nhật</param>
    /// <returns>Thông tin danh mục đã cập nhật</returns>
    [HttpPut("{categoryId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> UpdateCategory(long categoryId, [FromBody] UpdateCategoryDto updateCategoryDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<CategoryDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _categoryService.UpdateCategoryAsync(categoryId, updateCategoryDto);
        
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
    /// Xóa danh mục
    /// </summary>
    /// <param name="categoryId">ID danh mục</param>
    /// <returns>Kết quả xóa</returns>
    [HttpDelete("{categoryId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteCategory(long categoryId)
    {
        var result = await _categoryService.DeleteCategoryAsync(categoryId);
        
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
}
