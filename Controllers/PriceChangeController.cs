using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PriceChangeController : ControllerBase
{
    private readonly IPriceChangeService _priceChangeService;
    private readonly ILogger<PriceChangeController> _logger;

    public PriceChangeController(IPriceChangeService priceChangeService, ILogger<PriceChangeController> logger)
    {
        _priceChangeService = priceChangeService;
        _logger = logger;
    }

    /// <summary>
    /// Get price changes with filtering and pagination
    /// </summary>
    /// <param name="request">Search criteria</param>
    /// <returns>List of price changes</returns>
    [HttpGet]
    [Authorize(Roles = "ADMIN,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<PriceChangeListResponse>>> GetPriceChanges([FromQuery] PriceChangeSearchRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<PriceChangeListResponse>
            {
                Success = false,
                Message = "Invalid data",
                Errors = errors
            });
        }

        var result = await _priceChangeService.GetPriceChangesAsync(request);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Get price change by ID
    /// </summary>
    /// <param name="priceChangeId">Price change ID</param>
    /// <returns>Price change details</returns>
    [HttpGet("{priceChangeId}")]
    [Authorize(Roles = "ADMIN,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<PriceChangeDto>>> GetPriceChangeById(long priceChangeId)
    {
        var result = await _priceChangeService.GetPriceChangeByIdAsync(priceChangeId);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }

    /// <summary>
    /// Create new price change
    /// </summary>
    /// <param name="createPriceChangeDto">Price change data</param>
    /// <returns>Created price change</returns>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<PriceChangeDto>>> CreatePriceChange([FromBody] CreatePriceChangeDto createPriceChangeDto)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new ApiResponse<PriceChangeDto>
            {
                Success = false,
                Message = "Invalid data",
                Errors = errors
            });
        }

        // Get employee ID from claims
        var nameIdentifierClaims = User.Claims.Where(c => c.Type == ClaimTypes.NameIdentifier).ToList();
        string? accountIdClaim = null;
        foreach (var claim in nameIdentifierClaims)
        {
            if (long.TryParse(claim.Value, out _))
            {
                accountIdClaim = claim.Value;
                break;
            }
        }
        
        if (string.IsNullOrEmpty(accountIdClaim) || !long.TryParse(accountIdClaim, out long accountId))
        {
            return Unauthorized(new ApiResponse<PriceChangeDto>
            {
                Success = false,
                Message = "Cannot determine user from token",
                Errors = new List<string> { "Invalid token or missing accountId" }
            });
        }

        var result = await _priceChangeService.CreatePriceChangeAsync(createPriceChangeDto, accountId);
        
        if (result.Success)
        {
            return CreatedAtAction(nameof(GetPriceChangeById), new { priceChangeId = result.Data!.PriceChangeId }, result);
        }

        return BadRequest(result);
    }

    /// <summary>
    /// Get current price for a book
    /// </summary>
    /// <param name="isbn">Book ISBN</param>
    /// <param name="asOfDate">Date to check price (optional, defaults to now)</param>
    /// <returns>Current price</returns>
    [HttpGet("current-price/{isbn}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<decimal>>> GetCurrentPrice(string isbn, [FromQuery] DateTime? asOfDate = null)
    {
        var result = await _priceChangeService.GetCurrentPriceAsync(isbn, asOfDate);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }

    /// <summary>
    /// Get price history for a book
    /// </summary>
    /// <param name="isbn">Book ISBN</param>
    /// <returns>Price history</returns>
    [HttpGet("history/{isbn}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<PriceChangeDto>>>> GetPriceHistory(string isbn)
    {
        var result = await _priceChangeService.GetPriceHistoryAsync(isbn);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return NotFound(result);
    }
}









