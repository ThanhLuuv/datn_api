using System.Security.Claims;
using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "CUSTOMER")]
public class CustomerController : ControllerBase
{
    private readonly BookStoreDbContext _db;
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(BookStoreDbContext db, ILogger<CustomerController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Lấy thông tin cá nhân của khách hàng hiện tại (dựa trên token)
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> GetMyProfile()
    {
        var customer = await GetCustomerFromTokenAsync();
        if (customer == null)
        {
            return Unauthorized(new ApiResponse<CustomerProfileDto>
            {
                Success = false,
                Message = "Không xác định được khách hàng từ token",
                Errors = new List<string> { "Token không hợp lệ hoặc chưa tạo hồ sơ khách hàng" }
            });
        }

        var dto = MapToProfileDto(customer);

        return Ok(new ApiResponse<CustomerProfileDto>
        {
            Success = true,
            Message = "Lấy thông tin khách hàng thành công",
            Data = dto
        });
    }

    /// <summary>
    /// Cập nhật thông tin cá nhân của khách hàng hiện tại
    /// </summary>
    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> UpdateMyProfile([FromBody] UpdateCustomerProfileDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<CustomerProfileDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var customer = await GetCustomerFromTokenAsync();
        if (customer == null)
        {
            return Unauthorized(new ApiResponse<CustomerProfileDto>
            {
                Success = false,
                Message = "Không xác định được khách hàng từ token",
                Errors = new List<string> { "Token không hợp lệ hoặc chưa tạo hồ sơ khách hàng" }
            });
        }

        // Cập nhật các trường được phép
        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Gender = ParseGender(request.Gender);
        customer.DateOfBirth = request.DateOfBirth;
        customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        customer.Email = string.IsNullOrWhiteSpace(request.Email) ? customer.Email : request.Email.Trim();
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var dto = MapToProfileDto(customer);

        return Ok(new ApiResponse<CustomerProfileDto>
        {
            Success = true,
            Message = "Cập nhật thông tin khách hàng thành công",
            Data = dto
        });
    }

    private async Task<Customer?> GetCustomerFromTokenAsync()
    {
        try
        {
            // Lấy accountId từ claim NameIdentifier
            var accountIdClaim = User.Claims
                .Where(c => c.Type == ClaimTypes.NameIdentifier ||
                            c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                .FirstOrDefault(c => long.TryParse(c.Value, out _))
                ?.Value;

            if (string.IsNullOrEmpty(accountIdClaim) || !long.TryParse(accountIdClaim, out var accountId))
            {
                return null;
            }

            return await _db.Customers
                .FirstOrDefaultAsync(c => c.AccountId == accountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer from token");
            return null;
        }
    }

    private static CustomerProfileDto MapToProfileDto(Customer customer)
    {
        return new CustomerProfileDto
        {
            CustomerId = customer.CustomerId,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Gender = customer.Gender.ToString(),
            DateOfBirth = customer.DateOfBirth,
            Address = customer.Address,
            Phone = customer.Phone,
            Email = customer.Email,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
        };
    }

    private static Gender ParseGender(string gender)
    {
        if (Enum.TryParse<Gender>(gender, true, out var g)) return g;
        return Gender.Other;
    }
}


