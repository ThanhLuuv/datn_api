using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class CreatePaymentLinkRequestDto
{
	[Required]
	public long OrderId { get; set; }
	[Required]
	[Range(0.01, 999999999.99)]
	public decimal Amount { get; set; }
	public string Currency { get; set; } = "VND";
	public string? ReturnUrl { get; set; }
	public string? CancelUrl { get; set; }
}

public class CreatePaymentLinkResponseDto
{
	public long TransactionId { get; set; }
	public string CheckoutUrl { get; set; } = string.Empty;
	public string ProviderTxnId { get; set; } = string.Empty;
}


