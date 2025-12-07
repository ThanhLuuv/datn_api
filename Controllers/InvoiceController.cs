using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoiceController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    /// <summary>
    /// Lấy danh sách hóa đơn
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<InvoiceListResponse>>> GetInvoices([FromQuery] InvoiceSearchRequest request)
    {
        var result = await _invoiceService.GetInvoicesAsync(request);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Lấy danh sách hóa đơn chưa có phiếu trả kèm thông tin đơn hàng
    /// </summary>
    [HttpGet("with-orders")]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<InvoiceWithOrderListResponse>>> GetInvoicesWithOrders([FromQuery] InvoiceSearchRequest request)
    {
        var result = await _invoiceService.GetInvoicesWithOrdersAsync(request);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Lấy hóa đơn theo ID
    /// </summary>
    [HttpGet("{invoiceId}")]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetInvoice(long invoiceId)
    {
        var result = await _invoiceService.GetInvoiceByIdAsync(invoiceId);
        if (result.Success) return Ok(result);
        return NotFound(result);
    }

    /// <summary>
    /// Lấy hóa đơn theo Order ID
    /// </summary>
    [HttpGet("order/{orderId}")]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE,CUSTOMER")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetInvoiceByOrder(long orderId)
    {
        var result = await _invoiceService.GetInvoiceByOrderIdAsync(orderId);
        if (result.Success) return Ok(result);
        return NotFound(result);
    }

    /// <summary>
    /// Kiểm tra đơn hàng đã thanh toán chưa
    /// </summary>
    [HttpGet("check-payment/{orderId}")]
    [Authorize(Roles = "ADMIN,SALES_EMPLOYEE,EMPLOYEE,CUSTOMER")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckPayment(long orderId)
    {
        var hasPaid = await _invoiceService.HasPaidInvoiceAsync(orderId);
        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Message = hasPaid ? "Đơn hàng đã thanh toán" : "Đơn hàng chưa thanh toán",
            Data = hasPaid
        });
    }
}
