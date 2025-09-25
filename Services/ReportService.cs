using BookStore.Api.Data;
using BookStore.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class ReportService : IReportService
{
	private readonly BookStoreDbContext _context;

	public ReportService(BookStoreDbContext context)
	{
		_context = context;
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


