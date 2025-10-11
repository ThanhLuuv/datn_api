using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AreaController : ControllerBase
{
    private readonly BookStoreDbContext _context;

    public AreaController(BookStoreDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy danh sách tất cả khu vực (Public API)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AreaListResponse>>> GetAreas()
    {
        try
        {
            var areas = await _context.Areas
                .OrderBy(a => a.Name)
                .Select(a => new AreaDto
                {
                    AreaId = a.AreaId,
                    Name = a.Name,
                    Keywords = a.Keywords
                })
                .ToListAsync();

            var response = new AreaListResponse
            {
                Areas = areas,
                TotalCount = areas.Count
            };

            return Ok(new ApiResponse<AreaListResponse>
            {
                Success = true,
                Message = "Lấy danh sách khu vực thành công",
                Data = response
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<AreaListResponse>
            {
                Success = false,
                Message = "Lỗi khi lấy danh sách khu vực",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Lấy thông tin khu vực theo ID (Public API)
    /// </summary>
    [HttpGet("{areaId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AreaDto>>> GetArea(long areaId)
    {
        try
        {
            var area = await _context.Areas
                .Where(a => a.AreaId == areaId)
                .Select(a => new AreaDto
                {
                    AreaId = a.AreaId,
                    Name = a.Name,
                    Keywords = a.Keywords
                })
                .FirstOrDefaultAsync();

            if (area == null)
            {
                return NotFound(new ApiResponse<AreaDto>
                {
                    Success = false,
                    Message = "Không tìm thấy khu vực"
                });
            }

            return Ok(new ApiResponse<AreaDto>
            {
                Success = true,
                Message = "Lấy thông tin khu vực thành công",
                Data = area
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<AreaDto>
            {
                Success = false,
                Message = "Lỗi khi lấy thông tin khu vực",
                Errors = new List<string> { ex.Message }
            });
        }
    }
}
