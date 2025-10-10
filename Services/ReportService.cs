using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class ReportService : IReportService
{
	private readonly BookStoreDbContext _context;

	public ReportService(BookStoreDbContext context)
	{
		_context = context;
	}

        public async Task<ApiResponse<MonthlyRevenueReportResponse>> GetMonthlyRevenueAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var from = new DateTime(fromDate.Year, fromDate.Month, 1);
                var to = new DateTime(toDate.Year, toDate.Month, 1);
                if (to < from)
                {
                    return new ApiResponse<MonthlyRevenueReportResponse>
                    {
                        Success = false,
                        Message = "Khoảng tháng không hợp lệ",
                        Errors = new List<string> { "toDate phải lớn hơn hoặc bằng fromDate" }
                    };
                }

                var fromStr = from.ToString("yyyy-MM-dd");
                var toStr = to.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
                var rows = await _context.MonthlyRevenueRows
                    .FromSqlRaw("CALL sp_revenue_by_month({0}, {1})", fromStr, toStr)
                    .ToListAsync();

                var byMonth = rows.ToDictionary(x => (x.Year, x.Month), x => x.Revenue);
                var items = new List<RevenueByMonthDto>();
                var cursor = new DateTime(from.Year, from.Month, 1);
                var end = new DateTime(to.Year, to.Month, 1);
                while (cursor <= end)
                {
                    var key = (cursor.Year, cursor.Month);
                    items.Add(new RevenueByMonthDto
                    {
                        Year = cursor.Year,
                        Month = cursor.Month,
                        Revenue = byMonth.TryGetValue(key, out var rev) ? rev : 0m
                    });
                    cursor = cursor.AddMonths(1);
                }

                var total = items.Sum(i => i.Revenue);
                return new ApiResponse<MonthlyRevenueReportResponse>
                {
                    Success = true,
                    Message = "Lấy báo cáo doanh thu theo tháng thành công",
                    Data = new MonthlyRevenueReportResponse { Items = items, TotalRevenue = total }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<MonthlyRevenueReportResponse>
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi lấy báo cáo doanh thu theo tháng",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<ApiResponse<QuarterlyRevenueReportResponse>> GetQuarterlyRevenueAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var from = new DateTime(fromDate.Year, ((fromDate.Month - 1) / 3) * 3 + 1, 1);
                var to = new DateTime(toDate.Year, ((toDate.Month - 1) / 3) * 3 + 1, 1);
                if (to < from)
                {
                    return new ApiResponse<QuarterlyRevenueReportResponse>
                    {
                        Success = false,
                        Message = "Khoảng quý không hợp lệ",
                        Errors = new List<string> { "toDate phải lớn hơn hoặc bằng fromDate" }
                    };
                }

                var fromStr = from.ToString("yyyy-MM-dd");
                var toStr = to.AddMonths(3).AddDays(-1).ToString("yyyy-MM-dd");
                var rows = await _context.QuarterlyRevenueRows
                    .FromSqlRaw("CALL sp_revenue_by_quarter({0}, {1})", fromStr, toStr)
                    .ToListAsync();

                var byQuarter = rows.ToDictionary(x => (x.Year, x.Quarter), x => x.Revenue);
                var items = new List<RevenueByQuarterDto>();
                var cursor = from;
                var end = to;
                while (cursor <= end)
                {
                    var quarter = ((cursor.Month - 1) / 3) + 1;
                    var key = (cursor.Year, quarter);
                    items.Add(new RevenueByQuarterDto
                    {
                        Year = cursor.Year,
                        Quarter = quarter,
                        Revenue = byQuarter.TryGetValue(key, out var rev) ? rev : 0m
                    });
                    cursor = cursor.AddMonths(3);
                }

                var total = items.Sum(i => i.Revenue);
                return new ApiResponse<QuarterlyRevenueReportResponse>
                {
                    Success = true,
                    Message = "Lấy báo cáo doanh thu theo quý thành công",
                    Data = new QuarterlyRevenueReportResponse { Items = items, TotalRevenue = total }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<QuarterlyRevenueReportResponse>
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi lấy báo cáo doanh thu theo quý",
                    Errors = new List<string> { ex.Message }
                };
            }
        }
	public async Task<ApiResponse<RevenueReportResponse>> GetRevenueByDateRangeAsync(RevenueReportRequest request)
	{
		try
		{
			var from = request.FromDate.Date;
			var to = request.ToDate.Date;
			if (to < from)
			{
				return new ApiResponse<RevenueReportResponse>
				{
					Success = false,
					Message = "Khoảng ngày không hợp lệ",
					Errors = new List<string> { "ToDate phải lớn hơn hoặc bằng FromDate" }
				};
			}

            var query = await _context.Orders
				.Where(o => o.PlacedAt >= from && o.PlacedAt < to.AddDays(1) && o.Status == Models.OrderStatus.Delivered)
				.Select(o => new
				{
					Day = o.PlacedAt.Date,
					Revenue = o.OrderLines.Sum(ol => ol.Qty * ol.UnitPrice)
				})
				.GroupBy(x => x.Day)
				.Select(g => new RevenueByDayDto
				{
					Day = g.Key,
					Revenue = g.Sum(x => x.Revenue)
				})
				.OrderBy(x => x.Day)
				.ToListAsync();

            // Fill missing days with zero revenue
            var byDay = new Dictionary<DateTime, decimal>();
            foreach (var item in query)
            {
                byDay[item.Day.Date] = item.Revenue;
            }

            var items = new List<RevenueByDayDto>();
            var cursor = from.Date;
            while (cursor <= to.Date)
            {
                items.Add(new RevenueByDayDto
                {
                    Day = cursor,
                    Revenue = byDay.TryGetValue(cursor, out var rev) ? rev : 0m
                });
                cursor = cursor.AddDays(1);
            }

            var total = items.Sum(x => x.Revenue);
			return new ApiResponse<RevenueReportResponse>
			{
				Success = true,
				Message = "Lấy báo cáo doanh thu thành công",
                Data = new RevenueReportResponse { Items = items, TotalRevenue = total }
			};
		}
		catch (Exception ex)
		{
			return new ApiResponse<RevenueReportResponse>
			{
				Success = false,
				Message = "Đã xảy ra lỗi khi lấy báo cáo doanh thu",
				Errors = new List<string> { ex.Message }
			};
		}
	}
}


