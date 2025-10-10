using BookStore.Api.DTOs;
using BookStore.Api.Models;

namespace BookStore.Api.Services;

public interface IOrderService
{
	Task<ApiResponse<OrderListResponse>> GetOrdersAsync(OrderSearchRequest request);
	Task<ApiResponse<OrderDto>> GetOrderByIdAsync(long orderId);
	Task<ApiResponse<OrderDto>> ApproveOrderAsync(long orderId, ApproveOrderRequest request, string approverEmail);
	Task<ApiResponse<OrderDto>> AssignDeliveryAsync(long orderId, AssignDeliveryRequest request, string assignerEmail);
	Task<ApiResponse<OrderDto>> ConfirmDeliveredAsync(long orderId, ConfirmDeliveredRequest request, string confirmerEmail);

	Task<ApiResponse<List<SuggestedEmployeeDto>>> GetDeliveryCandidatesAsync(long orderId);
	Task<ApiResponse<OrderDto>> CreateOrderAsync(CreateOrderDto createOrderDto, long customerId);
	Task<Customer?> GetCustomerByAccountIdAsync(long accountId);
}


