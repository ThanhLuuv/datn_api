using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class DepartmentController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<DepartmentListResponse>>> GetDepartments([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? searchTerm = null)
    {
        var result = await _departmentService.GetDepartmentsAsync(pageNumber, pageSize, searchTerm);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("{departmentId}")]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> GetDepartment(long departmentId)
    {
        var result = await _departmentService.GetDepartmentByIdAsync(departmentId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> CreateDepartment([FromBody] CreateDepartmentDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ApiResponse<DepartmentDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
        }

        var result = await _departmentService.CreateDepartmentAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{departmentId}")]
    public async Task<ActionResult<ApiResponse<DepartmentDto>>> UpdateDepartment(long departmentId, [FromBody] UpdateDepartmentDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ApiResponse<DepartmentDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
        }

        var result = await _departmentService.UpdateDepartmentAsync(departmentId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{departmentId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDepartment(long departmentId)
    {
        var result = await _departmentService.DeleteDepartmentAsync(departmentId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}






