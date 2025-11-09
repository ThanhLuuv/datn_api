using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace BookStore.Api.Services;

public class ReportService : IReportService
{
	private readonly BookStoreDbContext _context;

	public ReportService(BookStoreDbContext context)
	{
		_context = context;
	}

    public async Task<ApiResponse<ProfitReportDto>> GetProfitReportAsync(DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
            var to = toDate ?? DateTime.UtcNow;

            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "CALL report_profit(@fromDate, @toDate)";
            var p1 = command.CreateParameter();
            p1.ParameterName = "@fromDate";
            p1.Value = from;
            command.Parameters.Add(p1);
            var p2 = command.CreateParameter();
            p2.ParameterName = "@toDate";
            p2.Value = to;
            command.Parameters.Add(p2);

            var dto = new ProfitReportDto();

            using (var reader = await command.ExecuteReaderAsync())
            {
                // Result set #1: summary
                if (await reader.ReadAsync())
                {
                    dto.FromDate = reader.IsDBNull(0) ? from.Date : reader.GetDateTime(0);
                    dto.ToDate = reader.IsDBNull(1) ? to.Date : reader.GetDateTime(1);
                    dto.OrdersCount = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
                    dto.Revenue = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                    dto.CostOfGoods = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
                    dto.OperatingExpenses = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
                }

                // Result set #2: top sold items
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dto.TopSoldItems.Add(new ProfitTopSoldItemDto
                        {
                            Isbn = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            QtySold = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                            Revenue = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                            Cogs = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                            Profit = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
                        });
                    }
                }

                // Result set #3: top margin items
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dto.TopMarginItems.Add(new ProfitTopMarginItemDto
                        {
                            Isbn = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            QtySold = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                            Revenue = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                            Cogs = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                            Profit = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                            MarginPct = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6)
                        });
                    }
                }
            }

            await connection.CloseAsync();

            return new ApiResponse<ProfitReportDto> { Success = true, Message = "Tính lợi nhuận thành công", Data = dto };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ProfitReportDto> { Success = false, Message = "Lỗi khi tính báo cáo lợi nhuận", Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<ApiResponse<BooksByCategoryResponse>> GetBooksByCategoryAsync()
    {
        try
        {
            // Count books by category name; fall back to 'Khác' when missing
            var query = await _context.Books
                .Include(b => b.Category)
                .GroupBy(b => b.Category != null ? b.Category.Name : "Khác")
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var total = query.Sum(x => x.Count);
            var items = query.Select(x => new BooksByCategoryItemDto
            {
                Category = x.Category ?? "Khác",
                Count = x.Count,
                Percent = total > 0 ? Math.Round((decimal)x.Count * 100m / total, 2) : 0m
            }).ToList();

            return new ApiResponse<BooksByCategoryResponse>
            {
                Success = true,
                Message = "Lấy tỷ lệ sách theo danh mục thành công",
                Data = new BooksByCategoryResponse { Total = total, Items = items }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<BooksByCategoryResponse>
            {
                Success = false,
                Message = "Lỗi khi lấy tỷ lệ sách theo danh mục",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<InventoryReportResponse>> GetInventoryAsOfDateAsync(DateTime reportDate)
    {
        try
        {
            var dateStr = reportDate.ToString("yyyy-MM-dd");
            
            // Execute stored procedure using raw SQL and map manually
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = "CALL SP_InventoryReport_AsOfDate(@reportDate)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@reportDate";
            parameter.Value = dateStr;
            command.Parameters.Add(parameter);
            
            var items = new List<InventoryReportItem>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                items.Add(new InventoryReportItem
                {
                    Category = reader.GetString(0),
                    Isbn = reader.GetString(1),
                    Title = reader.GetString(2),
                    QuantityOnHand = reader.GetInt32(3),
                    AveragePrice = reader.GetDecimal(4)
                });
            }
            
            await connection.CloseAsync();

            return new ApiResponse<InventoryReportResponse>
            {
                Success = true,
                Message = "Lấy báo cáo tồn kho theo ngày thành công",
                Data = new InventoryReportResponse { ReportDate = reportDate.Date, Items = items }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<InventoryReportResponse>
            {
                Success = false,
                Message = "Lỗi khi lấy báo cáo tồn kho",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<RecomputeAveragePriceResponse>> RecomputeAveragePriceAsync(string isbn)
    {
        try
        {
            // Call SP to recompute (no result mapping expected)
            await _context.Database.ExecuteSqlRawAsync("CALL SP_UpdateAveragePrice_Last4Receipts({0})", isbn);

            // SP returns a scalar; fetch updated book value instead
            var book = await _context.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Isbn == isbn);
            if (book == null)
            {
                return new ApiResponse<RecomputeAveragePriceResponse>
                {
                    Success = false,
                    Message = "Không tìm thấy sách",
                    Errors = new List<string> { "ISBN không tồn tại" }
                };
            }

            return new ApiResponse<RecomputeAveragePriceResponse>
            {
                Success = true,
                Message = "Cập nhật giá nhập bình quân thành công",
                Data = new RecomputeAveragePriceResponse { Isbn = isbn, AveragePrice = book.AveragePrice }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<RecomputeAveragePriceResponse>
            {
                Success = false,
                Message = "Lỗi khi cập nhật giá nhập bình quân",
                Errors = new List<string> { ex.Message }
            };
        }
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


