using BookStore.Api.DTOs;

namespace BookStore.Api.Services;

public interface IReportService
{
	Task<ApiResponse<RevenueReportResponse>> GetRevenueByDateRangeAsync(RevenueReportRequest request);
    Task<ApiResponse<MonthlyRevenueReportResponse>> GetMonthlyRevenueAsync(DateTime fromDate, DateTime toDate);
    Task<ApiResponse<QuarterlyRevenueReportResponse>> GetQuarterlyRevenueAsync(DateTime fromDate, DateTime toDate);
    Task<ApiResponse<InventoryReportResponse>> GetInventoryAsOfDateAsync(DateTime reportDate);
    Task<ApiResponse<RecomputeAveragePriceResponse>> RecomputeAveragePriceAsync(string isbn);
}


