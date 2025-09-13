using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Kiểm tra tình trạng tổng quan của ứng dụng
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        
        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
                data = entry.Value.Data,
                exception = entry.Value.Exception?.Message
            }),
            timestamp = DateTime.UtcNow
        };

        return report.Status == HealthStatus.Healthy 
            ? Ok(result) 
            : StatusCode(503, result);
    }

    /// <summary>
    /// Kiểm tra nhanh - chỉ trả về status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        
        return Ok(new 
        { 
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Thông tin về ứng dụng
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            application = "BookStore API",
            version = "1.0.0",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            machineName = Environment.MachineName,
            processId = Environment.ProcessId,
            timestamp = DateTime.UtcNow,
            uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime
        });
    }
}
