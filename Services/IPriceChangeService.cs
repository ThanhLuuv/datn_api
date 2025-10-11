using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IPriceChangeService
{
    Task<ApiResponse<PriceChangeListResponse>> GetPriceChangesAsync(PriceChangeSearchRequest request);
    Task<ApiResponse<PriceChangeDto>> GetPriceChangeByIdAsync(long priceChangeId);
    Task<ApiResponse<PriceChangeDto>> CreatePriceChangeAsync(CreatePriceChangeDto createPriceChangeDto, long employeeAccountId);
    Task<ApiResponse<decimal>> GetCurrentPriceAsync(string isbn, DateTime? asOfDate = null);
    Task<ApiResponse<List<PriceChangeDto>>> GetPriceHistoryAsync(string isbn);
}



