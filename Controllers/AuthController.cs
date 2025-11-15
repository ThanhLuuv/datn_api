using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Đăng ký tài khoản mới
    /// </summary>
    /// <param name="registerDto">Thông tin đăng ký</param>
    /// <returns>Kết quả đăng ký và JWT token</returns>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register([FromBody] RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _authService.RegisterAsync(registerDto);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Đăng nhập
    /// </summary>
    /// <param name="loginDto">Thông tin đăng nhập</param>
    /// <returns>Kết quả đăng nhập và JWT token</returns>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = errors
            });
        }

        var result = await _authService.LoginAsync(loginDto);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Refresh token - Lấy token mới với permissions mới nhất
    /// </summary>
    /// <returns>Token mới với permissions đầy đủ</returns>
    [HttpPost("refresh-token")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
        
        if (string.IsNullOrEmpty(email))
        {
            return Unauthorized(new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Không thể xác định người dùng",
                Errors = new List<string> { "Token không hợp lệ" }
            });
        }

        var result = await _authService.RefreshTokenAsync(email);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }
}
