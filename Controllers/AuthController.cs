using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
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

    /// <summary>
    /// Khởi tạo đăng nhập Google OAuth
    /// </summary>
    /// <returns>Redirect đến Google OAuth</returns>
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google/callback" };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Callback từ Google OAuth
    /// </summary>
    /// <returns>JWT token hoặc redirect với token</returns>
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        try
        {
            // Get the authentication result
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            
            if (!result.Succeeded)
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Đăng nhập Google thất bại",
                    Errors = new List<string> { "Không thể xác thực với Google" }
                });
            }

            // Extract user information from claims
            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value 
                ?? result.Principal.FindFirst("email")?.Value;
            var googleId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? result.Principal.FindFirst("sub")?.Value;
            var firstName = result.Principal.FindFirst(ClaimTypes.GivenName)?.Value 
                ?? result.Principal.FindFirst("given_name")?.Value;
            var lastName = result.Principal.FindFirst(ClaimTypes.Surname)?.Value 
                ?? result.Principal.FindFirst("family_name")?.Value;
            var pictureUrl = result.Principal.FindFirst("picture")?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Không thể lấy thông tin từ Google",
                    Errors = new List<string> { "Email hoặc Google ID không tồn tại" }
                });
            }

            // Call service to handle Google login
            var loginResult = await _authService.GoogleLoginAsync(email, googleId, firstName, lastName, pictureUrl);

            if (!loginResult.Success)
            {
                return BadRequest(loginResult);
            }

            // Sign out the Google authentication scheme
            await HttpContext.SignOutAsync(GoogleDefaults.AuthenticationScheme);

            // Return the JWT token in response
            return Ok(loginResult);
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi xử lý đăng nhập Google",
                Errors = new List<string> { ex.Message }
            });
        }
    }
}
