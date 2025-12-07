using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BookStore.Api.Models;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/customer/[controller]")]
[Authorize(Policy = "PERM_READ_RETURN")]
public class CustomerReturnController : ControllerBase
{
    private readonly IReturnService _returnService;
    private readonly IOrderService _orderService;
    private readonly ILogger<CustomerReturnController> _logger;

    public CustomerReturnController(IReturnService returnService, IOrderService orderService, ILogger<CustomerReturnController> logger)
    {
        _returnService = returnService;
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Customer tạo yêu cầu trả hàng
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "PERM_WRITE_RETURN")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> CreateReturn([FromBody] CreateReturnDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ApiResponse<ReturnDto> { Success = false, Message = "Dữ liệu không hợp lệ", Errors = errors });
        }

        // Lấy customer ID từ token
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<ReturnDto> { Success = false, Message = "Không xác định được khách hàng" });
        }

        // Kiểm tra Invoice có thuộc về customer này không
        var invoiceResult = await _orderService.GetInvoiceByOrderIdAsync(request.InvoiceId);
        if (!invoiceResult.Success)
        {
            return BadRequest(new ApiResponse<ReturnDto> { Success = false, Message = "Không tìm thấy hóa đơn" });
        }

        // TODO: Cần thêm logic kiểm tra Invoice có thuộc về customer này không
        // Hiện tại tạm thời cho phép tạo return

        var result = await _returnService.CreateReturnAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Customer xem danh sách yêu cầu trả hàng của mình
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<ReturnListResponse>>> GetMyReturns([FromQuery] ReturnSearchRequest request)
    {
        // Lấy customer ID từ token
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<ReturnListResponse> { Success = false, Message = "Không xác định được khách hàng" });
        }

        // TODO: Cần thêm logic filter theo customer ID
        // Hiện tại tạm thời trả về tất cả returns

        var result = await _returnService.GetReturnsAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Customer xem chi tiết yêu cầu trả hàng của mình
    /// </summary>
    [HttpGet("{returnId}")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> GetMyReturn(long returnId)
    {
        // Lấy customer ID từ token
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<ReturnDto> { Success = false, Message = "Không xác định được khách hàng" });
        }

        var result = await _returnService.GetReturnByIdAsync(returnId);
        if (!result.Success) return NotFound(result);

        // TODO: Cần thêm logic kiểm tra return có thuộc về customer này không
        // Hiện tại tạm thời cho phép xem

        return Ok(result);
    }

    /// <summary>
    /// Customer hủy yêu cầu trả hàng (chỉ khi status = PENDING)
    /// </summary>
    [HttpPut("{returnId}/cancel")]
    [Authorize(Policy = "PERM_WRITE_RETURN")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> CancelReturn(long returnId)
    {
        // Lấy customer ID từ token
        var customerId = await GetCustomerIdFromToken();
        if (customerId == null)
        {
            return Unauthorized(new ApiResponse<ReturnDto> { Success = false, Message = "Không xác định được khách hàng" });
        }

        // Kiểm tra return có tồn tại không
        var returnResult = await _returnService.GetReturnByIdAsync(returnId);
        if (!returnResult.Success)
        {
            return NotFound(new ApiResponse<ReturnDto> { Success = false, Message = "Không tìm thấy yêu cầu trả hàng" });
        }

        // TODO: Cần thêm logic kiểm tra return có thuộc về customer này không
        // Hiện tại tạm thời cho phép hủy

        // Chỉ cho phép hủy khi status = PENDING
        if (returnResult.Data?.Status != ReturnStatus.Pending)
        {
            return BadRequest(new ApiResponse<ReturnDto> { Success = false, Message = "Chỉ có thể hủy yêu cầu trả hàng đang chờ xử lý" });
        }

        var cancelRequest = new UpdateReturnStatusRequest
        {
            Status = ReturnStatus.Rejected,
            Notes = "Khách hàng hủy yêu cầu"
        };

        var result = await _returnService.UpdateReturnStatusAsync(returnId, cancelRequest, "CUSTOMER_CANCEL");
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    private async Task<long?> GetCustomerIdFromToken()
    {
        try
        {
            // Lấy account ID từ token
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
                return null;
            }

            // Tìm customer ID từ account ID
            var customer = await _orderService.GetCustomerByAccountIdAsync(accountId);
            return customer?.CustomerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer ID from token");
            return null;
        }
    }
}
