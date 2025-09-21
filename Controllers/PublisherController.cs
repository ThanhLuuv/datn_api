using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PublisherController : ControllerBase
{
    private readonly IPublisherService _publisherService;

    public PublisherController(IPublisherService publisherService)
    {
        _publisherService = publisherService;
    }

    /// <summary>
    /// Lấy danh sách nhà xuất bản
    /// </summary>
    /// <param name="pageNumber">Số trang (mặc định: 1)</param>
    /// <param name="pageSize">Kích thước trang (mặc định: 10)</param>
    /// <param name="searchTerm">Từ khóa tìm kiếm</param>
    /// <returns>Danh sách nhà xuất bản</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PublisherListResponse>>> GetPublishers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        var result = await _publisherService.GetPublishersAsync(pageNumber, pageSize, searchTerm);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Lấy thông tin nhà xuất bản theo ID
    /// </summary>
    /// <param name="publisherId">ID nhà xuất bản</param>
    /// <returns>Thông tin nhà xuất bản</returns>
    [HttpGet("{publisherId}")]
    public async Task<ActionResult<ApiResponse<PublisherDto>>> GetPublisher(long publisherId)
    {
        var result = await _publisherService.GetPublisherByIdAsync(publisherId);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }

    /// <summary>
    /// Tạo nhà xuất bản mới
    /// </summary>
    /// <param name="createPublisherDto">Thông tin nhà xuất bản mới</param>
    /// <returns>Thông tin nhà xuất bản đã tạo</returns>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<PublisherDto>>> CreatePublisher([FromBody] CreatePublisherDto createPublisherDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<PublisherDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _publisherService.CreatePublisherAsync(createPublisherDto);
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetPublisher), new { publisherId = result.Data.PublisherId }, result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Cập nhật nhà xuất bản
    /// </summary>
    /// <param name="publisherId">ID nhà xuất bản</param>
    /// <param name="updatePublisherDto">Thông tin cập nhật</param>
    /// <returns>Thông tin nhà xuất bản đã cập nhật</returns>
    [HttpPut("{publisherId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<PublisherDto>>> UpdatePublisher(long publisherId, [FromBody] UpdatePublisherDto updatePublisherDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<PublisherDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _publisherService.UpdatePublisherAsync(publisherId, updatePublisherDto);
        
        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message.Contains("không tìm thấy"))
        {
            return NotFound(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Xóa nhà xuất bản
    /// </summary>
    /// <param name="publisherId">ID nhà xuất bản</param>
    /// <returns>Kết quả xóa</returns>
    [HttpDelete("{publisherId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> DeletePublisher(long publisherId)
    {
        var result = await _publisherService.DeletePublisherAsync(publisherId);
        
        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message.Contains("không tìm thấy"))
        {
            return NotFound(result);
        }

        return BadRequest(result);
    }
}
