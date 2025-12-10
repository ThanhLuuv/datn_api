using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;
    private const string ProductionFrontendRedirect = "https://bookstore.thanhlaptrinh.online/#!/login";
    private const string LocalFrontendRedirect = "http://localhost:3000/#!/login";
    private static readonly string[] AllowedRedirectHosts = new[] { "bookstore.thanhlaptrinh.online", "datn.thanhlaptrinh.online", "localhost", "127.0.0.1" };

    // Google OAuth credentials - hardcoded as requested (not in config files)
    private const string GoogleClientId = "386583671447-j5196qdrpudv7kt4542urodse5hqeql0.apps.googleusercontent.com";
    private const string GoogleClientSecret = "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";
    
    // Get callback URL based on environment
    private string GetGoogleCallbackUrl()
    {
        // Production: callback về frontend domain (có reverse proxy forward đến API)
        if (!_environment.IsDevelopment())
        {
            return "https://api.thanhlaptrinh.online/api/auth/google/callback";
        }
        // Local: callback về backend port 5256
        return "http://localhost:5256/api/auth/google/callback";
    }

    public AuthController(IAuthService authService, IMemoryCache cache, IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
    {
        _authService = authService;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
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
    /// Yêu cầu đặt lại mật khẩu (Gửi email)
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<string>>> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        if (!ModelState.IsValid)
        {
             return BadRequest(new ApiResponse<string>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Đặt lại mật khẩu mới
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<string>>> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        if (!ModelState.IsValid)
        {
             return BadRequest(new ApiResponse<string>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var result = await _authService.ResetPasswordAsync(resetPasswordDto);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Khởi tạo đăng nhập Google OAuth
    /// </summary>
    /// <returns>Redirect đến Google OAuth</returns>
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        // Manual OAuth2 code flow to avoid correlation cookie issues.
        var redirect = Request.Query["redirect"].ToString();

        // Generate a state value and store the desired frontend redirect in memory for validation later.
        var state = Guid.NewGuid().ToString("N");
        _cache.Set(state, redirect, TimeSpan.FromMinutes(5));

        // Use callback URL based on environment
        var callbackUrl = GetGoogleCallbackUrl();
        var oauthUrl = QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
        {
            ["client_id"] = GoogleClientId,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["redirect_uri"] = callbackUrl,
            ["state"] = state,
            ["access_type"] = "online",
            ["prompt"] = "select_account"
        });

        return Redirect(oauthUrl);
    }

    /// <summary>
    /// Callback từ Google OAuth
    /// </summary>
    /// <returns>JWT token hoặc redirect với token</returns>
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? state)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Thiếu code hoặc state từ Google",
                    Errors = new List<string> { "Invalid callback parameters" }
                });
            }

            // Validate stored state
            if (!_cache.TryGetValue<string?>(state, out var frontendRedirect))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "state không hợp lệ hoặc đã hết hạn",
                    Errors = new List<string> { "Invalid state" }
                });
            }

            // Exchange code for tokens
            var callbackUrl = GetGoogleCallbackUrl();
            var tokenClient = _httpClientFactory.CreateClient();
            var tokenRequest = new Dictionary<string, string?>
            {
                ["code"] = code,
                ["client_id"] = GoogleClientId,
                ["client_secret"] = GoogleClientSecret,
                ["redirect_uri"] = callbackUrl,
                ["grant_type"] = "authorization_code"
            };

            var tokenResponse = await tokenClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenRequest!));
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errText = await tokenResponse.Content.ReadAsStringAsync();
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Không thể lấy token từ Google",
                    Errors = new List<string> { errText }
                });
            }

            using var tokenStream = await tokenResponse.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(tokenStream);
            var root = doc.RootElement;
            var accessToken = root.GetProperty("access_token").GetString();
            var idToken = root.TryGetProperty("id_token", out var idTokEl) ? idTokEl.GetString() : null;

            if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(idToken))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Google trả về access_token/id_token rỗng",
                    Errors = new List<string> { "Empty tokens" }
                });
            }

            // Get user info
            var userClient = _httpClientFactory.CreateClient();
            userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var userResp = await userClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            if (!userResp.IsSuccessStatusCode)
            {
                var errText = await userResp.Content.ReadAsStringAsync();
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Không thể lấy thông tin người dùng từ Google",
                    Errors = new List<string> { errText }
                });
            }

            var userJson = await userResp.Content.ReadAsStringAsync();
            using var userDoc = JsonDocument.Parse(userJson);
            var userRoot = userDoc.RootElement;
            var email = userRoot.GetProperty("email").GetString();
            var googleId = userRoot.GetProperty("id").GetString();
            var firstName = userRoot.TryGetProperty("given_name", out var givenEl) ? givenEl.GetString() : null;
            var lastName = userRoot.TryGetProperty("family_name", out var famEl) ? famEl.GetString() : null;
            var pictureUrl = userRoot.TryGetProperty("picture", out var picEl) ? picEl.GetString() : null;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Không thể lấy thông tin từ Google",
                    Errors = new List<string> { "Email hoặc Google ID không tồn tại" }
                });
            }

            var loginResult = await _authService.GoogleLoginAsync(email, googleId, firstName, lastName, pictureUrl);
            if (!loginResult.Success)
            {
                return BadRequest(loginResult);
            }

            // Remove used state
            _cache.Remove(state);

            // If client expects JSON, return it
            var expectsJson = Request.Headers["Accept"].Any(h => h != null && h.Contains("application/json", StringComparison.OrdinalIgnoreCase));
            if (expectsJson)
            {
                return Ok(loginResult);
            }

            if (loginResult.Data == null)
            {
                return BadRequest(new ApiResponse<AuthResponseDto>
                {
                    Success = false,
                    Message = "Không thể tạo token đăng nhập",
                    Errors = new List<string> { "Dữ liệu phản hồi trống" }
                });
            }

            var redirectUrl = BuildGoogleRedirectUrl(loginResult.Data, frontendRedirect);
            return Redirect(redirectUrl);
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

    private string BuildGoogleRedirectUrl(AuthResponseDto authData, string? frontendRedirectFromState = null)
    {
        // Use frontendRedirect from state if provided, otherwise resolve from request
        string redirectBase;
        if (!string.IsNullOrWhiteSpace(frontendRedirectFromState))
        {
            // If it's a full URL, use it directly (after validation)
            if (IsAllowedRedirect(frontendRedirectFromState))
            {
                redirectBase = frontendRedirectFromState;
            }
            // If it's a fragment like #!/login, prepend the base URL
            else if (frontendRedirectFromState.StartsWith("#"))
            {
                var isLocal = _environment.IsDevelopment() || 
                             Request.Host.Host.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                             Request.Host.Host.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                var baseUrl = isLocal ? LocalFrontendRedirect : ProductionFrontendRedirect;
                redirectBase = baseUrl.TrimEnd('/') + "/" + frontendRedirectFromState.TrimStart('/');
            }
            else
            {
                redirectBase = ResolveFrontendRedirectBase();
            }
        }
        else
        {
            redirectBase = ResolveFrontendRedirectBase();
        }

        var queryParams = new Dictionary<string, string?>
        {
            ["token"] = authData.Token,
            ["email"] = authData.Email,
            ["role"] = authData.Role,
            ["fullName"] = authData.FullName,
            ["expires"] = authData.Expires.ToString("o")
        };

        if (!string.IsNullOrWhiteSpace(authData.AvatarUrl))
        {
            queryParams["avatar"] = authData.AvatarUrl;
        }

        var queryString = BuildQueryString(queryParams);

        if (string.IsNullOrEmpty(queryString))
        {
            return redirectBase;
        }

        if (redirectBase.Contains("#"))
        {
            var hashIndex = redirectBase.IndexOf('#');
            var baseUrl = redirectBase.Substring(0, hashIndex);
            var hashPart = redirectBase.Substring(hashIndex + 1); // exclude '#'
            var separator = hashPart.Contains("?") ? "&" : "?";
            return $"{baseUrl}#{hashPart}{separator}{queryString}";
        }

        var baseSeparator = redirectBase.Contains("?") ? "&" : "?";
        return $"{redirectBase}{baseSeparator}{queryString}";
    }

    private static string BuildQueryString(Dictionary<string, string?> queryParams)
    {
        var encoded = queryParams
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}");

        return string.Join("&", encoded);
    }

    private string ResolveFrontendRedirectBase()
    {
        // Detect environment: check if we're in development or production
        var isLocal = _environment.IsDevelopment() || 
                     Request.Host.Host.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                     Request.Host.Host.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

        // If a redirect query param is provided, prefer it when it's safe
        if (Request.Query.TryGetValue("redirect", out var redirectCandidate))
        {
            var candidate = redirectCandidate.ToString();

            // If candidate is an absolute allowed URL, return it
            if (IsAllowedRedirect(candidate))
            {
                return candidate;
            }

            // If candidate is a relative path or fragment, map it to the frontend base
            if (!string.IsNullOrWhiteSpace(candidate) && (candidate.StartsWith("/") || candidate.StartsWith("#")))
            {
                var baseUrl = isLocal ? LocalFrontendRedirect : ProductionFrontendRedirect;

                // If candidate is a fragment like #!/..., append to base
                if (candidate.StartsWith("#"))
                {
                    return baseUrl.TrimEnd('/') + "/" + candidate.TrimStart('/');
                }

                return baseUrl.TrimEnd('/') + candidate;
            }
        }

        // Fallback to Referer header: if it's an allowed absolute URL use it or otherwise pick local/production base
        var refererHeader = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(refererHeader))
        {
            if (IsAllowedRedirect(refererHeader))
            {
                // If referer is from localhost, return the local frontend base
                if (refererHeader.Contains("localhost", StringComparison.OrdinalIgnoreCase) || refererHeader.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    return LocalFrontendRedirect;
                }

                // Otherwise, use the referer as-is (it's an allowed absolute url)
                return refererHeader;
            }
        }

        // Default: use environment-based redirect
        return isLocal ? LocalFrontendRedirect : ProductionFrontendRedirect;
    }

    private static bool IsAllowedRedirect(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return AllowedRedirectHosts.Any(host =>
            uri.Host.Contains(host, StringComparison.OrdinalIgnoreCase));
    }
}
