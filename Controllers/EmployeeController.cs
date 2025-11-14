using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeeController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<EmployeeListResponse>>> GetEmployees([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? searchTerm = null, [FromQuery] long? departmentId = null)
    {
        var result = await _employeeService.GetEmployeesAsync(pageNumber, pageSize, searchTerm, departmentId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("{employeeId}")]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> GetEmployee(long employeeId)
    {
        var result = await _employeeService.GetEmployeeByIdAsync(employeeId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost("create-with-account")]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> CreateEmployeeWithAccount([FromBody] CreateEmployeeWithAccountRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ApiResponse<EmployeeDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
        }

        var result = await _employeeService.CreateEmployeeWithAccountAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{employeeId}")]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> UpdateEmployee(long employeeId, [FromBody] UpdateEmployeeDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ApiResponse<EmployeeDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
        }

        var result = await _employeeService.UpdateEmployeeAsync(employeeId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{employeeId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEmployee(long employeeId)
    {
        var result = await _employeeService.DeleteEmployeeAsync(employeeId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}






