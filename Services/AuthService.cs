using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace BookStore.Api.Services;

public class AuthService : IAuthService
{
    private readonly BookStoreDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public AuthService(BookStoreDbContext context, IJwtService jwtService, IEmailService emailService, IMemoryCache cache, IConfiguration configuration)
    {
        _context = context;
        _jwtService = jwtService;
        _emailService = emailService;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto registerDto)
    {
        try
        {
            // Check if email already exists
            var existingUser = await _context.Accounts
                .Include(a => a.Customer)
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
            await _context.Entry(account)
                .Reference(a => a.Customer)
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
                    Expires = DateTime.UtcNow.AddDays(expireDays),
                    FullName = $"{customer.FirstName} {customer.LastName}".Trim(),
                    AvatarUrl = null
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
                .Include(a => a.Customer)
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

            var fullName = account.Customer != null
                ? $"{account.Customer.FirstName} {account.Customer.LastName}".Trim()
                : account.Email;

            return new ApiResponse<AuthResponseDto>
            {
                Success = true,
                Message = "Đăng nhập thành công",
                Data = new AuthResponseDto
                {
                    Token = token,
                    Email = account.Email,
                    Role = account.Role.Name,
                    Expires = DateTime.UtcNow.AddDays(expireDays),
                    FullName = fullName,
                    AvatarUrl = null
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

    public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(string email)
    {
        try
        {
            // Find user by email
            var account = await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Email == email);

            if (account == null)
            {
                return new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Không tìm thấy tài khoản",
                    Errors = new List<string> { "Email không tồn tại" }
                };
            }

            // Load permissions for role
            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == account.RoleId)
                .Select(rp => rp.Permission.Code)
                .ToListAsync();

            // Generate new JWT token with latest permissions
            var token = _jwtService.GenerateToken(account, permissions);
            var expireDays = 7; // Default from configuration

            var fullName = account.Customer != null
                ? $"{account.Customer.FirstName} {account.Customer.LastName}".Trim()
                : account.Email;

            return new ApiResponse<AuthResponseDto>
            {
                Success = true,
                Message = "Token đã được làm mới",
                Data = new AuthResponseDto
                {
                    Token = token,
                    Email = account.Email,
                    Role = account.Role.Name,
                    Expires = DateTime.UtcNow.AddDays(expireDays),
                    FullName = fullName,
                    AvatarUrl = null
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi làm mới token",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<AuthResponseDto>> GoogleLoginAsync(string email, string googleId, string? firstName, string? lastName, string? pictureUrl)
    {
        try
        {
            // Find existing account by email
            var account = await _context.Accounts
                .Include(a => a.Role)
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.Email == email);

            // If account doesn't exist, create new one
            if (account == null)
            {
                // Get default CUSTOMER role (RoleId = 1)
                var customerRole = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Name == "CUSTOMER");

                if (customerRole == null)
                {
                    return new ApiResponse<AuthResponseDto>
                    {
                        Success = false,
                        Message = "Không tìm thấy role CUSTOMER",
                        Errors = new List<string> { "Hệ thống chưa được cấu hình đúng" }
                    };
                }

                // Create new account with Google authentication
                // Use a placeholder password hash since the field is required
                // Google accounts won't use password authentication
                var passwordHash = BCrypt.Net.BCrypt.HashPassword($"GOOGLE_{googleId}_{DateTime.UtcNow.Ticks}");

                account = new Account
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    RoleId = customerRole.RoleId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                // Auto-create Customer profile
                var customer = new Customer
                {
                    AccountId = account.AccountId,
                    FirstName = firstName ?? email.Split('@')[0],
                    LastName = lastName ?? string.Empty,
                    Gender = Gender.Other,
                    Email = email,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                // Reload account with role
                await _context.Entry(account)
                    .Reference(a => a.Role)
                    .LoadAsync();
                await _context.Entry(account)
                    .Reference(a => a.Customer)
                    .LoadAsync();
            }

            if (account.Customer == null)
            {
                account.Customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.AccountId == account.AccountId);
            }

            if (account.Customer != null)
            {
                var updated = false;

                if (!string.IsNullOrWhiteSpace(firstName) && account.Customer.FirstName != firstName)
                {
                    account.Customer.FirstName = firstName!;
                    updated = true;
                }

                if (!string.IsNullOrWhiteSpace(lastName) && account.Customer.LastName != lastName)
                {
                    account.Customer.LastName = lastName!;
                    updated = true;
                }

                if (updated)
                {
                    account.Customer.UpdatedAt = DateTime.UtcNow;
                    _context.Customers.Update(account.Customer);
                    await _context.SaveChangesAsync();
                }
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
                Message = "Đăng nhập Google thành công",
                Data = new AuthResponseDto
                {
                    Token = token,
                    Email = account.Email,
                    Role = account.Role.Name,
                    Expires = DateTime.UtcNow.AddDays(expireDays),
                    FullName = account.Customer != null
                        ? $"{account.Customer.FirstName} {account.Customer.LastName}".Trim()
                        : account.Email,
                    AvatarUrl = pictureUrl
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponseDto>
            {
                Success = false,
                Message = "Đã xảy ra lỗi khi đăng nhập Google",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<string>> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        try
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Email == forgotPasswordDto.Email);

            if (account == null)
            {
                // To prevent email enumeration, we effectively return success but don't send email
                // or we can be explicit. For UX, explicit is often better but less secure.
                // User requirement: "comprehensive". I'll be nice and return "If email exists..."
                // But user specifically asked for functionality.
                return new ApiResponse<string>
                {
                    Success = true, 
                    Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được hướng dẫn đặt lại mật khẩu."
                };
            }

            if (!account.IsActive)
            {
                return new ApiResponse<string>
                {
                    Success = false,
                    Message = "Tài khoản đang bị khóa."
                };
            }

            // Generate token
            var token = Guid.NewGuid().ToString("N");
            
            // Store in cache for 15 minutes
            _cache.Set($"RESET_PASS_{token}", account.Email, TimeSpan.FromMinutes(15));

            // Generate Link
            // Retrieve frontend URL from config or use defaults
            var isProduction = _configuration["ASPNETCORE_ENVIRONMENT"] != "Development";
            var baseUrl = _configuration["FrontendUrl"] ?? "https://bookstore.thanhlaptrinh.online";
            var resetLink = $"{baseUrl}/#!/reset-password?token={token}";

            // Send Email
            var body = $@"
                <h3>Yêu cầu đặt lại mật khẩu</h3>
                <p>Bạn nhận được email này vì chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản: {account.Email}</p>
                <p>Vui lòng click vào link bên dưới để đặt lại mật khẩu (Link có hiệu lực trong 15 phút):</p>
                <p><a href='{resetLink}'>{resetLink}</a></p>
                <p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
            ";

            await _emailService.SendEmailAsync(account.Email, "Đặt lại mật khẩu BookStore", body);

            return new ApiResponse<string>
            {
                Success = true,
                Message = "Email hướng dẫn đặt lại mật khẩu đã được gửi."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<string>
            {
                Success = false,
                Message = "Lỗi xử lý quên mật khẩu: " + ex.Message
            };
        }
    }

    public async Task<ApiResponse<string>> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        try
        {
            if (!_cache.TryGetValue($"RESET_PASS_{resetPasswordDto.Token}", out string? email) || string.IsNullOrEmpty(email))
            {
                return new ApiResponse<string>
                {
                    Success = false,
                    Message = "Link đặt lại mật khẩu không hợp lệ hoặc đã hết hạn."
                };
            }

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
            if (account == null)
            {
                return new ApiResponse<string>
                {
                    Success = false,
                    Message = "Tài khoản không tồn tại."
                };
            }

            // Update password
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
            account.UpdatedAt = DateTime.UtcNow;

            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();

            // Clear token
            _cache.Remove($"RESET_PASS_{resetPasswordDto.Token}");

            return new ApiResponse<string>
            {
                Success = true,
                Message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập với mật khẩu mới."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<string>
            {
                Success = false,
                Message = "Lỗi đặt lại mật khẩu: " + ex.Message
            };
        }
    }
}
