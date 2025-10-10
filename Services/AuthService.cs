using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace BookStore.Api.Services;

public class AuthService : IAuthService
{
    private readonly BookStoreDbContext _context;
    private readonly IJwtService _jwtService;

    public AuthService(BookStoreDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto registerDto)
    {
        try
        {
            // Check if email already exists
            var existingUser = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Email == registerDto.Email);

            if (existingUser != null)
            {
                return new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Email đã tồn tại trong hệ thống",
                    Errors = new List<string> { "Email đã được sử dụng" }
                };
            }

            // Check if role exists
            var role = await _context.Roles.FindAsync((long)registerDto.RoleId);
            if (role == null)
            {
                return new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Role không tồn tại",
                    Errors = new List<string> { "Role ID không hợp lệ" }
                };
            }

            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            // Create new account
            var account = new Account
            {
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                RoleId = registerDto.RoleId
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            // Auto-create Customer profile using email as name
            var customer = new Customer
            {
                AccountId = account.AccountId,
                FirstName = registerDto.Email,
                LastName = registerDto.Email,
                Gender = Gender.Other,
                Email = registerDto.Email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            // Load role for token generation
            await _context.Entry(account)
                .Reference(a => a.Role)
                .LoadAsync();

            // Load permissions for role
            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == account.RoleId)
                .Select(rp => rp.Permission.Code)
                .ToListAsync();

            // Generate JWT token with permissions
            var token = _jwtService.GenerateToken(account, permissions);
            var expireDays = 7; // Default from configuration
            
            return new ApiResponse<AuthResponseDto>
            {
                Success = true,
                Message = "Đăng ký thành công",
                Data = new AuthResponseDto
                {
                    Token = token,
                    Email = account.Email,
                    Role = account.Role.Name,
                    Expires = DateTime.UtcNow.AddDays(expireDays)
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi đăng ký",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto loginDto)
    {
        try
        {
            // Find user by email
            var account = await _context.Accounts
                .Include(a => a.Role)
                .FirstOrDefaultAsync(a => a.Email == loginDto.Email);

            if (account == null)
            {
                return new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Email hoặc mật khẩu không đúng",
                    Errors = new List<string> { "Thông tin đăng nhập không hợp lệ" }
                };
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, account.PasswordHash))
            {
                return new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Email hoặc mật khẩu không đúng",
                    Errors = new List<string> { "Thông tin đăng nhập không hợp lệ" }
                };
            }

            // Load permissions for role
            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == account.RoleId)
                .Select(rp => rp.Permission.Code)
                .ToListAsync();

            // Generate JWT token with permissions
            var token = _jwtService.GenerateToken(account, permissions);
            var expireDays = 7; // Default from configuration

            return new ApiResponse<AuthResponseDto>
            {
                Success = true,
                Message = "Đăng nhập thành công",
                Data = new AuthResponseDto
                {
                    Token = token,
                    Email = account.Email,
                    Role = account.Role.Name,
                    Expires = DateTime.UtcNow.AddDays(expireDays)
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi đăng nhập",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
