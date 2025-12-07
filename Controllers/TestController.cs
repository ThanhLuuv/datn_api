using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BookStore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly BookStoreDbContext _db;

    public TestController(BookStoreDbContext db)
    {
        _db = db;
    }
    /// <summary>
    /// Endpoint công khai - không cần authentication
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            message = "Đây là endpoint công khai, ai cũng có thể truy cập",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Endpoint yêu cầu authentication
    /// </summary>
    [HttpPost]
    [Authorize]
    public IActionResult Post()
    {
        var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("email")?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            message = "Bạn đã đăng nhập thành công!",
            email = userEmail,
            role = userRole,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Endpoint công khai - không cần authentication
    /// </summary>
    [HttpGet("public")]
    public IActionResult GetPublic()
    {
        return Ok(new
        {
            message = "Đây là endpoint công khai, ai cũng có thể truy cập",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("employees")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEmployees([FromQuery] string? email)
    {
        var query = _db.Employees.AsQueryable();
        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(e => e.Email == email);
        }
        var list = await query
            .Select(e => new { e.EmployeeId, e.AccountId, e.DepartmentId, e.FirstName, e.LastName, e.Email })
            .ToListAsync();
        return Ok(new { count = list.Count, items = list });
    }

    [HttpGet("accounts")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAccounts([FromQuery] string? email)
    {
        var query = _db.Accounts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(a => a.Email == email);
        }
        var list = await query
            .Select(a => new { a.AccountId, a.Email, a.RoleId, a.IsActive })
            .ToListAsync();
        return Ok(new { count = list.Count, items = list });
    }

    /// <summary>
    /// Endpoint yêu cầu authentication
    /// </summary>
    [HttpGet("protected")]
    [Authorize]
    public IActionResult GetProtected()
    {
        var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("email")?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            message = "Bạn đã đăng nhập thành công!",
            email = userEmail,
            role = userRole,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Endpoint chỉ dành cho ADMIN
    /// </summary>
    [HttpGet("admin-only")]
    [Authorize(Policy = "PERM_READ_REPORT")]
    public IActionResult GetAdminOnly()
    {
        var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("email")?.Value;

        return Ok(new
        {
            message = "Chỉ ADMIN mới có thể truy cập endpoint này",
            email = userEmail,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Endpoint dành cho nhân viên bán hàng và admin
    /// </summary>
    [HttpGet("sales-only")]
    [Authorize(Policy = "PERM_READ_REPORT")]
    public IActionResult GetSalesOnly()
    {
        var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("email")?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            message = "Endpoint dành cho nhân viên bán hàng và admin",
            email = userEmail,
            role = userRole,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Endpoint dành cho nhân viên giao hàng và admin
    /// </summary>
    [HttpGet("delivery-only")]
    [Authorize(Policy = "PERM_READ_ORDER")]
    public IActionResult GetDeliveryOnly()
    {
        var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("email")?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            message = "Endpoint dành cho nhân viên giao hàng và admin",
            email = userEmail,
            role = userRole,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Endpoint dành cho tất cả nhân viên và admin
    /// </summary>
    [HttpGet("staff-only")]
    [Authorize(Policy = "PERM_READ_ORDER")]
    public IActionResult GetStaffOnly()
    {
        var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("email")?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            message = "Endpoint dành cho tất cả nhân viên và admin",
            email = userEmail,
            role = userRole,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Debug endpoint - Kiểm tra permissions trong token
    /// </summary>
    [HttpGet("debug-permissions")]
    [Authorize]
    public IActionResult DebugPermissions()
    {
        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var accountId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Get all claims
        var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        
        // Get permissions claim
        var permissionsClaim = User.Claims.FirstOrDefault(c => c.Type == "permissions");
        var permissions = permissionsClaim?.Value?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        
        // Check specific permission
        var hasReadReport = User.HasClaim(c => c.Type == "permissions" && ($" {c.Value} ").Contains($" READ_REPORT "));
        
        return Ok(new
        {
            email = userEmail,
            role = userRole,
            accountId = accountId,
            hasReadReportPermission = hasReadReport,
            permissionsCount = permissions.Length,
            permissions = permissions,
            allClaims = allClaims
        });
    }
}
