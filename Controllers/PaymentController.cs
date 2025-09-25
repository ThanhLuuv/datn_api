using BookStore.Api.DTOs;
using BookStore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
	private readonly IPaymentService _paymentService;

	public PaymentController(IPaymentService paymentService)
	{
		_paymentService = paymentService;
	}

	/// <summary>
	/// Tạo liên kết thanh toán PayOS cho đơn hàng
	/// </summary>
	[HttpPost("create-link")]
	[Authorize]
	public async Task<ActionResult<ApiResponse<CreatePaymentLinkResponseDto>>> CreateLink([FromBody] CreatePaymentLinkRequestDto request)
	{
		var result = await _paymentService.CreatePaymentLinkAsync(request);
		if (result.Success) return Ok(result);
		return BadRequest(result);
	}

	/// <summary>
	/// Webhook PayOS
	/// </summary>
	[HttpPost("webhook")]
	[AllowAnonymous]
	public async Task<IActionResult> Webhook()
	{
		using var reader = new StreamReader(Request.Body);
		var body = await reader.ReadToEndAsync();
		var signature = Request.Headers["x-signature"].ToString();
		await _paymentService.HandleWebhookAsync(body, signature);
		return Ok();
	}
}


