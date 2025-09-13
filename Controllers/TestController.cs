using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
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
    [Authorize(Roles = "ADMIN")]
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
    /// Endpoint dành cho EMPLOYEE và ADMIN
    /// </summary>
    [HttpGet("staff-only")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    public IActionResult GetStaffOnly()
    {
        var userEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("email")?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        return Ok(new
        {
            message = "Endpoint dành cho nhân viên và admin",
            email = userEmail,
            role = userRole,
            timestamp = DateTime.UtcNow
        });
    }
}
