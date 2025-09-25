using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IPaymentService
{
	Task<ApiResponse<CreatePaymentLinkResponseDto>> CreatePaymentLinkAsync(CreatePaymentLinkRequestDto request);
	Task HandleWebhookAsync(string payload, string signature);
}


