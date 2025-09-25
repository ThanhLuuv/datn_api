using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IReportService
{
	Task<ApiResponse<RevenueReportResponse>> GetRevenueByDateRangeAsync(RevenueReportRequest request);
}


