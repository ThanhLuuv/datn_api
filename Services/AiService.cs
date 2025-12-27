using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text.Json;
using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BookStore.Api.Services;

public class AiService : IAiService
{
    private readonly BookStoreDbContext _db;
    private readonly IReportService _reportService;
    private readonly IGeminiClient _geminiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiService> _logger;
    private static readonly JsonSerializerOptions CamelCaseSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    private static readonly IReadOnlyList<object> AdminTableSchemas = new object[]
    {
        new
        {
            table = "order",
            primaryKey = "OrderId",
            description = "Đơn hàng bán ra cho khách",
            columns = new[]
            {
                "OrderId","CustomerId","OrderDate","Status","TotalAmount","PaymentStatus","PaymentMethod","ShippingFee","CreatedAt","UpdatedAt"
            }
        },
        new
        {
            table = "order_detail",
            primaryKey = "OrderDetailId",
            description = "Chi tiết từng sách trong đơn hàng",
            columns = new[]
            {
                "OrderDetailId","OrderId","Isbn","Quantity","UnitPrice","Discount","SubTotal"
            }
        },
        new
        {
            table = "book",
            primaryKey = "Isbn",
            description = "Danh mục sách, tồn kho, giá",
            columns = new[]
            {
                "Isbn","Title","CategoryId","PublisherId","AveragePrice","PublishYear","Stock","Status","CreatedAt","UpdatedAt"
            }
        },
        new
        {
            table = "category",
            primaryKey = "CategoryId",
            description = "Thể loại sách",
            columns = new[]
            {
                "CategoryId","Name","Description","CreatedAt"
            }
        },
        new
        {
            table = "customer",
            primaryKey = "CustomerId",
            description = "Khách hàng mua sách",
            columns = new[]
            {
                "CustomerId","FirstName","LastName","Email","Phone","CreatedAt","Tier"
            }
        },
        new
        {
            table = "purchase_order",
            primaryKey = "PurchaseOrderId",
            description = "Phiếu nhập hàng từ nhà cung cấp",
            columns = new[]
            {
                "PurchaseOrderId","SupplierId","OrderDate","ExpectedDate","Status","TotalCost"
            }
        },
        new
        {
            table = "inventory_snapshot",
            primaryKey = "SnapshotId",
            description = "Ảnh chụp tồn kho định kỳ",
            columns = new[]
            {
                "SnapshotId","Isbn","QuantityOnHand","RecordedAt"
            }
        }
    };

    public AiService(
        BookStoreDbContext db,
        IReportService reportService,
        IGeminiClient geminiClient,
        IConfiguration configuration,
        ILogger<AiService> logger)
    {
        _db = db;
        _reportService = reportService;
        _geminiClient = geminiClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<AiBookRecommendationResponse>> GetBookRecommendationsAsync(
        AiBookRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return new ApiResponse<AiBookRecommendationResponse>
            {
                Success = false,
                Message = "Prompt is required",
                Errors = new List<string> { "Prompt không được để trống" }
            };
        }

        var maxResults = Math.Clamp(request.MaxResults, 3, 20);

        // 1) Lấy danh sách ứng viên từ DB dựa trên search đơn giản (title, category, publisher, author)
        var normalizedSearch = request.Prompt.Trim();

        var baseBooksQuery = _db.Books
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.AuthorBooks)
                .ThenInclude(ab => ab.Author)
            .Where(b => b.Status)
            .AsQueryable();

        var candidatesQuery = baseBooksQuery;

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            candidatesQuery = candidatesQuery.Where(b =>
                EF.Functions.Like(b.Title, $"%{normalizedSearch}%") ||
                EF.Functions.Like(b.Isbn, $"%{normalizedSearch}%") ||
                (b.Category != null && EF.Functions.Like(b.Category.Name, $"%{normalizedSearch}%")) ||
                (b.Publisher != null && EF.Functions.Like(b.Publisher.Name, $"%{normalizedSearch}%")) ||
                b.AuthorBooks.Any(ab =>
                    EF.Functions.Like(ab.Author.FirstName + " " + ab.Author.LastName, $"%{normalizedSearch}%")));
        }

        // Ưu tiên sách bán chạy nhất trong 90 ngày gần đây
        var since = DateTime.UtcNow.AddDays(-90);
        var bestSellerScores = await _db.OrderLines
            .Include(ol => ol.Order)
            .Where(ol => ol.Order.Status == OrderStatus.Delivered && ol.Order.PlacedAt >= since)
            .GroupBy(ol => ol.Isbn)
            .Select(g => new { Isbn = g.Key, Qty = g.Sum(x => x.Qty) })
            .ToDictionaryAsync(x => x.Isbn, x => x.Qty, cancellationToken);

        var candidateBooksRaw = await candidatesQuery
            .Take(120) // giới hạn để tránh prompt quá dài
            .ToListAsync(cancellationToken);

        if (candidateBooksRaw.Count < maxResults)
        {
            var existingIsbn = new HashSet<string>(candidateBooksRaw.Select(b => b.Isbn));
            var backupBooks = await baseBooksQuery
                .Where(b => !existingIsbn.Contains(b.Isbn))
                .Take(150)
                .ToListAsync(cancellationToken);
            candidateBooksRaw.AddRange(backupBooks);
        }

        if (candidateBooksRaw.Count == 0)
        {
            candidateBooksRaw = await baseBooksQuery
                .Take(150)
                .ToListAsync(cancellationToken);
        }

        // Chuẩn hoá dữ liệu gửi lên AI
        var candidatePayload = candidateBooksRaw.Select(b => new
        {
            isbn = b.Isbn,
            title = b.Title,
            category = b.Category?.Name,
            publisher = b.Publisher?.Name,
            publishYear = b.PublishYear,
            averagePrice = b.AveragePrice,
            totalSold90d = bestSellerScores.TryGetValue(b.Isbn, out var qty) ? qty : 0,
            authors = b.AuthorBooks.Select(ab => ab.Author.FirstName + " " + ab.Author.LastName).ToList()
        }).ToList();

        var systemPrompt = @"Bạn là trợ lý tư vấn sách cho nhà sách Việt Nam.
        Nhiệm vụ:
        - Đọc nhu cầu khách hàng và danh sách sách ứng viên.
        - Chọn ra tối đa N cuốn phù hợp nhất.
        - Với mỗi cuốn, viết tóm tắt ngắn (2‑4 câu) và nêu lý do vì sao phù hợp (1‑2 câu).
        - Ưu tiên sách bán chạy (totalSold90d cao), phù hợp chủ đề, năm xuất bản còn mới, và giá phù hợp.

        TRẢ LỜI DUY NHẤT DƯỚI DẠNG JSON hợp lệ theo schema:
        {
        ""recommendations"": [
            {
            ""isbn"": ""..."",
            ""aiSummary"": ""tóm tắt nội dung & đánh giá"",
            ""aiReason"": ""lý do cuốn này phù hợp"",
            ""score"": 0-100
            }
        ],
        ""overallSummary"": ""tóm tắt chung, tối đa 3 câu""
        }";

        var userPrompt = new
        {
            type = "book_recommendation",
            language = "vi",
            userRequest = request.Prompt,
            maxResults,
            candidates = candidatePayload
        };

        var aiResultJson = await _geminiClient.CallGeminiAsync(
            systemPrompt,
            JsonSerializer.Serialize(userPrompt),
            cancellationToken);

        if (aiResultJson == null)
        {
            // fallback: không có AI, trả về danh sách lọc đơn giản
            var fallbackBooks = candidateBooksRaw
                .OrderByDescending(b => bestSellerScores.TryGetValue(b.Isbn, out var qty) ? qty : 0)
                .Take(maxResults)
                .Select(MapBookToDto)
                .ToList();

            return new ApiResponse<AiBookRecommendationResponse>
            {
                Success = true,
                Message = "Gợi ý sách (fallback, không dùng AI do lỗi kết nối)",
                Data = new AiBookRecommendationResponse
                {
                    Books = fallbackBooks,
                    Summary = "Không kết nối được tới dịch vụ AI, hệ thống tạm gợi ý các sách bán chạy gần đây."
                }
            };
        }

        // Parse JSON từ AI
        var booksByIsbn = candidateBooksRaw.ToDictionary(b => b.Isbn, b => b);
        var recommendedDtos = new List<BookDto>();
        string? overallSummary = null;

        try
        {
            // Loại bỏ markdown code block nếu có (```json ... ``` hoặc ``` ... ```)
            var cleanedJson = aiResultJson.Trim();
            
            // Xử lý markdown code block: ```json hoặc ```
            if (cleanedJson.StartsWith("```"))
            {
                // Tìm dòng đầu tiên (có thể là ```json hoặc ```)
                var firstNewline = cleanedJson.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    cleanedJson = cleanedJson.Substring(firstNewline + 1);
                }
                else
                {
                    // Nếu không có newline, tìm sau ```json hoặc ```
                    var markerEnd = cleanedJson.IndexOf("```", 3);
                    if (markerEnd >= 0)
                    {
                        cleanedJson = cleanedJson.Substring(markerEnd + 3);
                    }
                    else if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanedJson = cleanedJson.Substring(7);
                    }
                    else if (cleanedJson.StartsWith("```"))
                    {
                        cleanedJson = cleanedJson.Substring(3);
                    }
                }
            }
            
            // Loại bỏ closing ``` ở cuối
            cleanedJson = cleanedJson.TrimEnd();
            if (cleanedJson.EndsWith("```"))
            {
                cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3).TrimEnd();
            }
            
            // Loại bỏ các ký tự markdown còn sót lại
            cleanedJson = cleanedJson.Trim();

            using var doc = JsonDocument.Parse(cleanedJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("overallSummary", out var overallSummaryProp) &&
                overallSummaryProp.ValueKind == JsonValueKind.String)
            {
                overallSummary = overallSummaryProp.GetString();
            }

            if (root.TryGetProperty("recommendations", out var recsElem) &&
                recsElem.ValueKind == JsonValueKind.Array)
            {
                var recs = recsElem.EnumerateArray()
                    .Select(e =>
                    {
                        var isbn = e.TryGetProperty("isbn", out var iProp) && iProp.ValueKind == JsonValueKind.String
                            ? iProp.GetString()
                            : null;
                        var aiSummary = e.TryGetProperty("aiSummary", out var sProp) && sProp.ValueKind == JsonValueKind.String
                            ? sProp.GetString()
                            : null;
                        var aiReason = e.TryGetProperty("aiReason", out var rProp) && rProp.ValueKind == JsonValueKind.String
                            ? rProp.GetString()
                            : null;
                        var score = e.TryGetProperty("score", out var scProp) && scProp.ValueKind is JsonValueKind.Number
                            ? scProp.GetDouble()
                            : 0;
                        return new { isbn, aiSummary, aiReason, score };
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.isbn))
                    .OrderByDescending(x => x.score)
                    .Take(maxResults)
                    .ToList();

                foreach (var rec in recs)
                {
                    if (rec.isbn != null && booksByIsbn.TryGetValue(rec.isbn, out var b))
                    {
                        var dto = MapBookToDto(b);
                        dto.AiSummary = rec.aiSummary;
                        dto.AiReason = rec.aiReason;
                        recommendedDtos.Add(dto);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI recommendation JSON. Raw content: {Content}", aiResultJson);
        }

        if (recommendedDtos.Count == 0)
        {
            // Nếu JSON không parse được, fallback: lấy top N ứng viên
            recommendedDtos = candidateBooksRaw
                .OrderByDescending(b => bestSellerScores.TryGetValue(b.Isbn, out var qty) ? qty : 0)
                .Take(maxResults)
                .Select(MapBookToDto)
                .ToList();
        }

        return new ApiResponse<AiBookRecommendationResponse>
        {
            Success = true,
            Message = "Gợi ý sách thành công",
            Data = new AiBookRecommendationResponse
            {
                Books = recommendedDtos,
                Summary = overallSummary
            }
        };
    }

    public async Task<ApiResponse<AdminAiAssistantResponse>> GetAdminInsightsAsync(
        AdminAiAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        // Luôn phân tích mặc định trên 30 ngày gần nhất nếu client không truyền,
        // để tránh phụ thuộc vào "trong tháng" của các API báo cáo bên dưới.
        var nowUtcDate = DateTime.UtcNow.Date;
        var defaultFrom = nowUtcDate.AddDays(-30);
        var from = request.FromDate?.Date ?? defaultFrom;
        var to = request.ToDate?.Date ?? nowUtcDate;

        // 1) Lấy báo cáo lợi nhuận để có TopSoldItems / TopMarginItems
        var profitReport = await _reportService.GetProfitReportAsync(from, to);
        if (!profitReport.Success || profitReport.Data == null)
        {
            return new ApiResponse<AdminAiAssistantResponse>
            {
                Success = false,
                Message = "Không thể lấy dữ liệu báo cáo lợi nhuận để phân tích",
                Errors = profitReport.Errors
            };
        }

        // 2) Lấy thống kê đánh giá của khách hàng cho các ISBN trong top bán chạy
        var topIsbns = profitReport.Data.TopSoldItems
            .Select(x => x.Isbn)
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Distinct()
            .ToList();

        var ratingStats = await _db.Ratings
            .Where(r => topIsbns.Contains(r.Isbn))
            .GroupBy(r => r.Isbn)
            .Select(g => new
            {
                Isbn = g.Key,
                AvgStars = g.Average(r => r.Stars),
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.Isbn, x => new { x.AvgStars, x.Count }, cancellationToken);

        // 3) Lấy thêm thông tin tồn kho hiện tại cho các ISBN trong top bán chạy
        var inventoryStats = await _db.Books
            .Where(b => topIsbns.Contains(b.Isbn))
            .Select(b => new
            {
                b.Isbn,
                b.Stock
            })
            .ToDictionaryAsync(x => x.Isbn, x => x.Stock, cancellationToken);

        // 4) Chuẩn bị payload cho AI (có bổ sung tồn kho & cờ low-stock trên từng sách bán chạy)
        var payload = new
        {
            type = "admin_assistant",
            language = string.IsNullOrWhiteSpace(request.Language) ? "vi" : request.Language.ToLowerInvariant(),
            period = new
            {
                from = from,
                to = to
            },
            profitSummary = new
            {
                profitReport.Data.OrdersCount,
                profitReport.Data.Revenue,
                profitReport.Data.CostOfGoods,
                profitReport.Data.OperatingExpenses,
                profit = profitReport.Data.Profit
            },
            // các sách bán chạy (dùng để phân tích xu hướng & đưa ra khuyến nghị nhập thêm)
            // CHỈ lấy tối đa 5 sách bán chạy nhất để tránh payload quá lớn và bám đúng yêu cầu business.
            topSoldItems = profitReport.Data.TopSoldItems
                .OrderByDescending(i => i.QtySold)
                .ThenByDescending(i => i.Revenue)
                .Take(5)
                .Select(i => new
            {
                i.Isbn,
                i.Title,
                i.QtySold,
                i.Revenue,
                i.Cogs,
                i.Profit,
                stock = inventoryStats.TryGetValue(i.Isbn, out var stockTs) ? stockTs : (int?)null,
                // isLowStock: true nếu tồn kho hiện tại dưới 15 cuốn
                isLowStock = inventoryStats.TryGetValue(i.Isbn, out var stockTs2) && stockTs2 < 15,
                ratings = ratingStats.TryGetValue(i.Isbn, out var rs)
                    ? new { avgStars = rs.AvgStars, count = rs.Count }
                    : null
            }).ToList(),
            // các sách biên lợi nhuận cao (giới hạn tối đa 5 mục để tham khảo thêm)
            topMarginItems = profitReport.Data.TopMarginItems
                .OrderByDescending(i => i.MarginPct)
                .ThenByDescending(i => i.Profit)
                .Take(5)
                .Select(i => new
            {
                i.Isbn,
                i.Title,
                i.QtySold,
                i.Revenue,
                i.Cogs,
                i.Profit,
                i.MarginPct,
                stock = inventoryStats.TryGetValue(i.Isbn, out var stockTm) ? stockTm : (int?)null,
                isLowStock = inventoryStats.TryGetValue(i.Isbn, out var stockTm2) && stockTm2 < 15,
                ratings = ratingStats.TryGetValue(i.Isbn, out var rs)
                    ? new { avgStars = rs.AvgStars, count = rs.Count }
                    : null
            }).ToList()
        };

        var systemPrompt = @"Bạn là trợ lý phân tích dữ liệu bán hàng cho nhà sách.
Input: 
- danh sách sách bán chạy (topSoldItems: với thông tin QtySold, Revenue, Profit, stock, isLowStock),
- danh sách sách biên lợi nhuận cao (topMarginItems),
- lợi nhuận tổng thể,
- thống kê đánh giá khách hàng,
- tồn kho hiện tại.

Nhiệm vụ (rất quan trọng – phải làm đúng thứ tự và đúng cấu trúc JSON yêu cầu):

1) TRƯỚC TIÊN, hãy duyệt qua danh sách topSoldItems:
   - Đây đã là tối đa 5 sách bán chạy nhất do backend lọc sẵn.
   - Với MỖI sách trong topSoldItems, nếu isbn không rỗng, hãy tạo MỘT mục tương ứng trong mảng bookSuggestions để phân tích và khuyến nghị.
   - Với các mục này:
     + Luôn điền đúng isbn (vì đây là sách đã có sẵn trong hệ thống).
     + Trong field ""reason"", BẮT BUỘC phải:
       * Tóm tắt sách đó đang bán chạy như thế nào (dựa trên QtySold / Revenue / Profit trong giai đoạn được cung cấp).
       * Nêu rõ tồn kho hiện tại (stock) còn bao nhiêu cuốn nếu có dữ liệu stock.
       * Nếu isLowStock = true (tức stock < 15): thêm cảnh báo tồn kho và gợi ý cụ thể nên nhập thêm khoảng bao nhiêu cuốn (dựa trên tốc độ bán và mức tồn hiện tại).
       * Nếu isLowStock = false: ghi rõ rằng tồn kho hiện vẫn an toàn, có thể CHƯA cần nhập gấp nhưng nên tiếp tục theo dõi xu hướng.

2) SAU KHI ĐÃ TẠO ĐẦY ĐỦ các bookSuggestions cho những sách đang có trong topSoldItems, bạn CÓ THỂ gợi ý THÊM TỐI ĐA 3 tựa sách MỚI CHƯA CÓ trong kho:
   - Với sách mới, để isbn = """" (chuỗi rỗng).
   - Tổng số sách mới (isbn rỗng) trong mảng bookSuggestions không được vượt quá 3.
   - Lý do nên tập trung vào khoảng trống danh mục, xu hướng thị trường, hoặc nhu cầu tiềm năng chưa được đáp ứng trong dữ liệu hiện tại.

3) Gợi ý những danh mục (thể loại) nên ưu tiên nhập thêm dựa trên:
   - các nhóm sách đang bán chạy,
   - các nhóm sách có biên lợi nhuận tốt,
   - và các mảng nội dung còn thiếu hụt.

4) Tổng hợp các nhận xét nổi bật từ đánh giá (ưu/nhược điểm) và đề xuất cải thiện dịch vụ/chất lượng.

TRẢ LỜI DUY NHẤT DƯỚI DẠNG JSON HỢP LỆ, TUÂN THỦ CÁC QUY TẮC SAU:
- Không được bao bọc JSON trong ```json``` hoặc bất kỳ markdown code block nào.
- Không được thêm bất kỳ text nào bên ngoài JSON (không giải thích thêm, không prefix/suffix).
- Không được có dấu phẩy thừa ở cuối phần tử mảng hoặc cuối object.
- Không được thêm comment, xuống dòng tự do bên ngoài cấu trúc JSON.

Dạng JSON bắt buộc:
{
  ""overview"": ""tóm tắt chung về tình hình bán hàng"",
  ""recommendedCategories"": [""..."", ""...""],
  ""bookSuggestions"": [
    {
      ""isbn"": ""hoặc rỗng nếu là sách mới"",
      ""title"": ""tên sách đề xuất"",
      ""category"": ""thể loại dự kiến"",
      ""reason"": ""lý do nên nhập / nhập thêm""
    }
  ],
  ""customerFeedbackSummary"": ""tổng hợp các ý chính từ đánh giá và gợi ý cải thiện""
}";

        var aiResultJson = await _geminiClient.CallGeminiAsync(
            systemPrompt,
            JsonSerializer.Serialize(payload),
            cancellationToken);

        if (aiResultJson == null)
        {
            return new ApiResponse<AdminAiAssistantResponse>
            {
                Success = false,
                Message = "Không thể kết nối tới dịch vụ AI để sinh gợi ý",
                Errors = new List<string> { "AI service unavailable" }
            };
        }

        var response = new AdminAiAssistantResponse();

        try
        {
            var normalizedJson = StripCodeFence(aiResultJson);
            using var doc = JsonDocument.Parse(normalizedJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("overview", out var ov) && ov.ValueKind == JsonValueKind.String)
            {
                response.Overview = ov.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("recommendedCategories", out var catsElem) &&
                catsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in catsElem.EnumerateArray())
                {
                    if (c.ValueKind == JsonValueKind.String)
                    {
                        var val = c.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            response.RecommendedCategories.Add(val.Trim());
                        }
                    }
                }
            }

            if (root.TryGetProperty("bookSuggestions", out var suggElem) &&
                suggElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in suggElem.EnumerateArray())
                {
                    var suggestion = new AdminAiBookSuggestion
                    {
                        Isbn = s.TryGetProperty("isbn", out var isbnProp) && isbnProp.ValueKind == JsonValueKind.String
                            ? isbnProp.GetString()
                            : null,
                        Title = s.TryGetProperty("title", out var tProp) && tProp.ValueKind == JsonValueKind.String
                            ? (tProp.GetString() ?? string.Empty)
                            : string.Empty,
                        Category = s.TryGetProperty("category", out var catProp) && catProp.ValueKind == JsonValueKind.String
                            ? catProp.GetString()
                            : null,
                        Reason = s.TryGetProperty("reason", out var rProp) && rProp.ValueKind == JsonValueKind.String
                            ? (rProp.GetString() ?? string.Empty)
                            : string.Empty,
                        MarketPrice = s.TryGetProperty("marketPrice", out var mpProp) && mpProp.ValueKind == JsonValueKind.String
                            ? mpProp.GetString()
                            : null,
                        MarketSourceName = s.TryGetProperty("marketSourceName", out var msnProp) && msnProp.ValueKind == JsonValueKind.String
                            ? msnProp.GetString()
                            : null,
                        MarketSourceUrl = s.TryGetProperty("marketSourceUrl", out var msuProp) && msuProp.ValueKind == JsonValueKind.String
                            ? msuProp.GetString()
                            : null,
                        SuggestedIsbn = s.TryGetProperty("suggestedIsbn", out var siProp) && siProp.ValueKind == JsonValueKind.String
                            ? siProp.GetString()
                            : null,
                        SuggestedCategoryId = s.TryGetProperty("suggestedCategoryId", out var scidProp) && scidProp.ValueKind == JsonValueKind.String
                            ? scidProp.GetString()
                            : null,
                        AuthorName = s.TryGetProperty("authorName", out var authorProp) && authorProp.ValueKind == JsonValueKind.String
                            ? authorProp.GetString()
                            : null,
                        PublisherName = s.TryGetProperty("publisherName", out var pubProp) && pubProp.ValueKind == JsonValueKind.String
                            ? pubProp.GetString()
                            : null,
                        PageCount = TryGetIntProperty(s, "pageCount"),
                        PublishYear = TryGetIntProperty(s, "publishYear"),
                        SuggestedPrice = TryGetDecimalProperty(s, "suggestedPrice"),
                        SuggestedStock = TryGetIntProperty(s, "suggestedStock"),
                        Description = s.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                            ? descProp.GetString()
                            : null
                    };

                    if (!string.IsNullOrWhiteSpace(suggestion.Title))
                    {
                        response.BookSuggestions.Add(suggestion);
                    }
                }
            }

            if (root.TryGetProperty("customerFeedbackSummary", out var fb) &&
                fb.ValueKind == JsonValueKind.String)
            {
                response.CustomerFeedbackSummary = fb.GetString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse admin AI assistant JSON. Raw content: {Content}", aiResultJson);
            // Trường hợp parse lỗi, vẫn trả về text gốc vào overview cho admin đọc
            response.Overview = "Không thể parse JSON từ AI. Nội dung thô:\n" + aiResultJson;
        }

        if (response.BookSuggestions.Count > 0)
        {
            // Chỉ enrich cho sách mới (isbn rỗng). Không tự động gọi tra cứu giá thị trường ở đây.
            // Việc tra cứu giá thị trường được tách ra thành API riêng `GetMarketPriceInsightsAsync`.
            var newBookSuggestions = response.BookSuggestions
                .Where(s => string.IsNullOrWhiteSpace(s.Isbn))
                .ToList();

            if (newBookSuggestions.Count > 0)
            {
                await EnrichBookSuggestionsAsync(newBookSuggestions, cancellationToken);

                // Sau khi enrich, đảm bảo mỗi suggestion có ISBN và SuggestedPrice nếu có thể
                foreach (var suggestion in newBookSuggestions)
                {
                    suggestion.SuggestedIsbn ??= await GenerateUniqueIsbnAsync(null, cancellationToken);
                    suggestion.SuggestedPrice ??= TryParseVndPrice(suggestion.MarketPrice);
                }
            }
        }

        // Log AI output for debugging
        _logger.LogInformation(
            "\n" +
            "╔════════════════════════════════════════════════════════════════════════════╗\n" +
            "║                    ADMIN ASSISTANT AI RESPONSE                             ║\n" +
            "╠════════════════════════════════════════════════════════════════════════════╣\n" +
            "║ Period: {FromDate} to {ToDate}\n" +
            "╠────────────────────────────────────────────────────────────────────────────╣\n" +
            "║ Overview:\n" +
            "║ {Overview}\n" +
            "╠────────────────────────────────────────────────────────────────────────────╣\n" +
            "║ Recommended Categories ({CategoryCount}):\n" +
            "║ {Categories}\n" +
            "╠────────────────────────────────────────────────────────────────────────────╣\n" +
            "║ Book Suggestions ({BookCount}):\n" +
            "║ {BookSuggestions}\n" +
            "╠────────────────────────────────────────────────────────────────────────────╣\n" +
            "║ Customer Feedback Summary:\n" +
            "║ {FeedbackSummary}\n" +
            "╚════════════════════════════════════════════════════════════════════════════╝",
            from.ToString("yyyy-MM-dd"),
            to.ToString("yyyy-MM-dd"),
            response.Overview,
            response.RecommendedCategories.Count,
            string.Join(", ", response.RecommendedCategories),
            response.BookSuggestions.Count,
            string.Join("\n║   ", response.BookSuggestions.Select(b => 
                $"- {b.Title} ({b.Isbn ?? "NEW"}) | {b.Category} | {b.Reason}")),
            response.CustomerFeedbackSummary);

        return new ApiResponse<AdminAiAssistantResponse>
        {
            Success = true,
            Message = "Sinh báo cáo gợi ý AI thành công",
            Data = response
        };
    }

    public async Task<ApiResponse<AdminAiChatResponse>> GetAdminChatAnswerAsync(
        AdminAiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Messages == null || request.Messages.Count == 0)
        {
            return new ApiResponse<AdminAiChatResponse>
            {
                Success = false,
                Message = "Cần cung cấp tối thiểu một tin nhắn trong hội thoại.",
                Errors = new List<string> { "messages is required" }
            };
        }

        var lastUserMessage = request.Messages
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

        if (lastUserMessage == null || string.IsNullOrWhiteSpace(lastUserMessage.Content))
        {
            return new ApiResponse<AdminAiChatResponse>
            {
                Success = false,
                Message = "Không tìm thấy câu hỏi của người dùng trong hội thoại.",
                Errors = new List<string> { "user message is required" }
            };
        }

        var from = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = request.ToDate ?? DateTime.UtcNow;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var language = string.IsNullOrWhiteSpace(request.Language)
            ? "vi"
            : request.Language.Trim().ToLowerInvariant();

        var trimmedHistory = request.Messages
            .TakeLast(12)
            .Select(m => new
            {
                role = (m.Role ?? "user").ToLowerInvariant(),
                content = (m.Content ?? string.Empty).Trim()
            })
            .Where(m => !string.IsNullOrWhiteSpace(m.content))
            .ToList();

        var answerResult = await GenerateAdminAnswerAsync(
            lastUserMessage.Content,
            from,
            to,
            request.IncludeInventorySnapshot,
            request.IncludeCategoryShare,
            language,
            trimmedHistory,
            cancellationToken);

        var assistantMessage = new AdminAiChatMessage
        {
            Role = "assistant",
            Content = answerResult.PlainText
        };

        var updatedMessages = request.Messages
            .Select(m => new AdminAiChatMessage
            {
                Role = m.Role,
                Content = m.Content
            })
            .ToList();
        updatedMessages.Add(assistantMessage);

        return new ApiResponse<AdminAiChatResponse>
        {
            Success = true,
            Message = "Trả lời thành công",
            Data = new AdminAiChatResponse
            {
                Messages = updatedMessages,
                PlainTextAnswer = answerResult.PlainText,
                MarkdownAnswer = answerResult.Markdown,
                DataSources = answerResult.DataSources,
                Insights = new Dictionary<string, object>
                {
                    ["timeframe"] = new { fromUtc = from, toUtc = to },
                    ["dataSources"] = answerResult.DataSources
                }
            }
        };
    }

    private async Task<AdminAiAnswerResult> GenerateAdminAnswerAsync(
        string question,
        DateTime from,
        DateTime to,
        bool includeInventorySnapshot,
        bool includeCategoryShare,
        string language,
        IEnumerable<object> conversation,
        CancellationToken cancellationToken)
    {
        var (dataPayload, dataSources) = await BuildAdminDataSnapshotAsync(
            from,
            to,
            includeInventorySnapshot,
            includeCategoryShare,
            cancellationToken);
        var schemaDescriptor = GetAdminTableSchemas();

        var planTask = BuildAiSqlPlanAsync(
            question,
            conversation,
            dataPayload,
            schemaDescriptor,
            language,
            cancellationToken);

        List<AiSqlResult> sqlResults = new();
        AiSqlPlan? plan = null;
        try
        {
            plan = await planTask;
            if (plan != null && plan.Steps.Count > 0)
            {
                sqlResults = await ExecuteAiSqlPlanAsync(plan, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI SQL plan failed. Falling back to summary only.");
        }

        var finalAnswer = await BuildAiFinalAnswerAsync(
            question,
            conversation,
            dataPayload,
            schemaDescriptor,
            plan,
            sqlResults,
            language,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(finalAnswer))
        {
            finalAnswer = "Xin lỗi, tôi chưa thể tìm thấy câu trả lời phù hợp từ dữ liệu hiện có.";
        }

        var formattedAnswer = BuildChatAnswerFormatting(finalAnswer);
        var plainTextAnswer = string.IsNullOrWhiteSpace(formattedAnswer.PlainText)
            ? finalAnswer
            : formattedAnswer.PlainText;
        var markdownAnswer = string.IsNullOrWhiteSpace(formattedAnswer.Markdown)
            ? finalAnswer
            : formattedAnswer.Markdown;

        return new AdminAiAnswerResult(
            plainTextAnswer,
            markdownAnswer,
            dataSources.ToList());
    }

    private async Task<AiSqlPlan?> BuildAiSqlPlanAsync(
        string question,
        IEnumerable<object> conversation,
        object dataset,
        IReadOnlyList<object> schema,
        string language,
        CancellationToken cancellationToken)
    {
        var systemPrompt = @"Bạn là AI lập kế hoạch truy vấn cho trợ lý dữ liệu BookStore.
Nhiệm vụ:
- Phân tích câu hỏi và lịch sử hội thoại.
- Nếu cần thêm dữ liệu, hãy đề xuất các truy vấn SELECT cụ thể (tối đa 2).
- Mỗi truy vấn phải có alias duy nhất và mô tả ngắn.
- Luôn trả về JSON với cấu trúc:
{
  ""summary"": ""..."",
  ""steps"": [
    { ""alias"": ""latest_order"", ""description"": ""..."", ""sql"": ""SELECT ..."" }
  ]
}
- Nếu dataset đã đủ để trả lời, trả về ""steps"": [] và đặt summary mô tả lý do.";

        var payload = new
        {
            question,
            language,
            conversation,
            dataset,
            schema
        };

        var planJson = await _geminiClient.CallGeminiAsync(systemPrompt, JsonSerializer.Serialize(payload), cancellationToken);
        if (planJson == null)
        {
            return null;
        }

        var normalized = StripCodeFence(planJson);
        try
        {
            using var doc = JsonDocument.Parse(normalized);
            var root = doc.RootElement;
            var summary = root.TryGetProperty("summary", out var summaryProp) && summaryProp.ValueKind == JsonValueKind.String
                ? summaryProp.GetString() ?? string.Empty
                : string.Empty;

            var steps = new List<AiSqlPlanStep>();
            if (root.TryGetProperty("steps", out var stepsProp) && stepsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var stepElem in stepsProp.EnumerateArray())
                {
                    var alias = stepElem.TryGetProperty("alias", out var aliasProp) && aliasProp.ValueKind == JsonValueKind.String
                        ? aliasProp.GetString()
                        : null;
                    var description = stepElem.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                        ? descProp.GetString()
                        : null;
                    var sql = stepElem.TryGetProperty("sql", out var sqlProp) && sqlProp.ValueKind == JsonValueKind.String
                        ? sqlProp.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(sql))
                    {
                        continue;
                    }

                    steps.Add(new AiSqlPlanStep(alias.Trim(), description?.Trim() ?? string.Empty, sql.Trim()));
                }
            }

            return new AiSqlPlan(summary, steps);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI SQL plan. Raw: {Plan}", normalized);
            return null;
        }
    }

    private async Task<List<AiSqlResult>> ExecuteAiSqlPlanAsync(
        AiSqlPlan plan,
        CancellationToken cancellationToken)
    {
        const int MaxRows = 25;
        var results = new List<AiSqlResult>();
        foreach (var step in plan.Steps)
        {
            if (!ValidateAiSql(step.Sql))
            {
                _logger.LogWarning("AI SQL step rejected (alias={Alias}). SQL: {Sql}", step.Alias, step.Sql);
                continue;
            }

            try
            {
                var rows = await ExecuteSqlQueryAsync(step.Sql, MaxRows, cancellationToken);
                results.Add(new AiSqlResult(step.Alias, step.Description, rows));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to execute AI SQL step {Alias}", step.Alias);
            }
        }

        return results;
    }

    private async Task<string?> BuildAiFinalAnswerAsync(
        string question,
        IEnumerable<object> conversation,
        object dataset,
        IReadOnlyList<object> schema,
        AiSqlPlan? plan,
        List<AiSqlResult> sqlResults,
        string language,
        CancellationToken cancellationToken)
    {
        var systemPrompt = @"Bạn là trợ lý dữ liệu BookStore.
Bạn sẽ nhận:
- câu hỏi,
- dataset tổng hợp,
- schema,
- kết quả thực thi các truy vấn SQL (nếu có).

Nhiệm vụ:
1. Trả lời trực tiếp câu hỏi dựa trên dữ liệu đã có và kết quả truy vấn.
2. Không liệt kê truy vấn SQL hay alias.
3. Nếu không đủ dữ liệu, giải thích ngắn gọn lý do.
4. Trả lời bằng tiếng Việt, tối đa 4 đoạn/bullet, chỉ plain text/markdown.";

        var payload = new
        {
            question,
            language,
            conversation,
            dataset,
            schema,
            plan = plan == null ? null : new
            {
                plan.Summary,
                steps = plan.Steps.Select(s => new { s.Alias, s.Description })
            },
            sqlResults = sqlResults.Select(r => new
            {
                r.Alias,
                r.Description,
                rows = r.Rows
            })
        };

        var response = await _geminiClient.CallGeminiAsync(systemPrompt, JsonSerializer.Serialize(payload), cancellationToken);
        return response == null ? null : StripCodeFence(response);
    }

    public async Task<ApiResponse<AdminAiVoiceResponse>> GetAdminVoiceAnswerAsync(
        AdminAiVoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AudioBase64))
        {
            return new ApiResponse<AdminAiVoiceResponse>
            {
                Success = false,
                Message = "Thiếu dữ liệu audio",
                Errors = new List<string> { "AudioBase64 is required" }
            };
        }

        var from = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = request.ToDate ?? DateTime.UtcNow;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var language = string.IsNullOrWhiteSpace(request.Language)
            ? "vi"
            : request.Language.Trim().ToLowerInvariant();
        var voiceName = Environment.GetEnvironmentVariable("Gemini__Voice")
            ?? _configuration["Gemini:Voice"]
            ?? "Zephyr";
        var mimeType = string.IsNullOrWhiteSpace(request.MimeType)
            ? "audio/webm"
            : request.MimeType!;
        var transcript = await TranscribeVoiceInputAsync(request.AudioBase64, mimeType, language, cancellationToken);
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return new ApiResponse<AdminAiVoiceResponse>
            {
                Success = false,
                Message = "Không thể nhận dạng giọng nói.",
                Errors = new List<string> { "transcription_failed" }
            };
        }

        var conversationHistory = new List<object>
        {
            new { role = "user", content = transcript }
        };

        var answerResult = await GenerateAdminAnswerAsync(
            transcript,
            from,
            to,
            request.IncludeInventorySnapshot,
            request.IncludeCategoryShare,
            language,
            conversationHistory,
            cancellationToken);

        var (audioBase64, audioMimeType) = await SynthesizeVoiceAnswerAsync(
            answerResult.PlainText,
            voiceName,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(audioBase64))
        {
            return new ApiResponse<AdminAiVoiceResponse>
            {
                Success = false,
                Message = "Không thể tạo audio phản hồi.",
                Errors = new List<string> { "tts_failed" }
            };
        }

        return new ApiResponse<AdminAiVoiceResponse>
        {
            Success = true,
            Message = "Voice assistant trả lời thành công",
            Data = new AdminAiVoiceResponse
            {
                Transcript = transcript,
                AnswerText = answerResult.PlainText,
                AudioBase64 = audioBase64,
                AudioMimeType = audioMimeType ?? "audio/wav",
                DataSources = answerResult.DataSources
            }
        };
    }

    private async Task<string?> TranscribeVoiceInputAsync(
        string audioBase64,
        string mimeType,
        string language,
        CancellationToken cancellationToken)
    {
        var prompt = $"Bạn là trình chuyển giọng nói thành văn bản cho quản trị viên BookStore. Ngôn ngữ chính: {(language == "vi" ? "tiếng Việt" : "ngôn ngữ người dùng yêu cầu")}. Trả lời duy nhất bằng văn bản thuần, không thêm nhận xét.";

        var body = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = prompt }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = audioBase64
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                candidateCount = 1,
                responseMimeType = "text/plain"
            }
        };

        var doc = await _geminiClient.CallGeminiCustomAsync(body, null, null, null, cancellationToken);
        if (doc == null)
        {
            return null;
        }

        using (doc)
        {
            return StripCodeFence(_geminiClient.ExtractFirstTextFromResponse(doc) ?? string.Empty);
        }
    }

    private async Task<(string? AudioBase64, string? MimeType)> SynthesizeVoiceAnswerAsync(
        string answerText,
        string voiceName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(answerText))
        {
            return (null, null);
        }

        var body = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = "Bạn là trợ lý trả lời giọng nói cho quản trị viên BookStore. Chuyển văn bản sau thành lời nói tự tin, rõ ràng." }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = answerText }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                candidateCount = 1,
                responseMimeType = "audio/pcm",
                speechConfig = new
                {
                    voiceConfig = new
                    {
                        prebuiltVoiceConfig = new
                        {
                            voiceName
                        }
                    }
                }
            }
        };

        var doc = await _geminiClient.CallGeminiCustomAsync(body, null, null, null, cancellationToken);
        if (doc == null)
        {
            return (null, null);
        }

        using (doc)
        {
            return ExtractAudioFromResponse(doc);
        }
    }

    public async Task<ApiResponse<AdminAiImportBookResponse>> ImportAiSuggestedBookAsync(
        AdminAiImportBookRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new ApiResponse<AdminAiImportBookResponse>
            {
                Success = false,
                Message = "Tiêu đề sách không được để trống",
                Errors = new List<string> { "Title is required" }
            };
        }

        var isbn = await GenerateUniqueIsbnAsync(request.Isbn, cancellationToken);
        var categoryId = await ResolveOrCreateCategoryAsync(request.CategoryId, request.CategoryName, cancellationToken);
        var publisherId = await ResolveOrCreatePublisherAsync(request.PublisherId, request.PublisherName, cancellationToken);
        var author = await ResolveOrCreateAuthorAsync(request.AuthorName, cancellationToken);

        if (categoryId == null)
        {
            return new ApiResponse<AdminAiImportBookResponse>
            {
                Success = false,
                Message = "Không thể xác định danh mục cho sách này",
                Errors = new List<string> { "Category not found" }
            };
        }

        if (publisherId == null)
        {
            return new ApiResponse<AdminAiImportBookResponse>
            {
                Success = false,
                Message = "Không thể xác định nhà xuất bản",
                Errors = new List<string> { "Publisher not found" }
            };
        }

        var pageCount = Math.Max(request.PageCount ?? 220, 30);
        var publishYear = request.PublishYear ?? DateTime.UtcNow.Year;
        var price = Math.Max(request.SuggestedPrice ?? 100_000m, 10_000m);
        var stock = Math.Max(request.Stock ?? 0, 0);

        var book = new Book
        {
            Isbn = isbn,
            Title = request.Title.Trim(),
            PageCount = pageCount,
            AveragePrice = price,
            PublishYear = publishYear,
            CategoryId = categoryId.Value,
            PublisherId = publisherId.Value,
            Stock = stock,
            Status = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        _db.Books.Add(book);
        await _db.SaveChangesAsync(cancellationToken);

        if (author != null)
        {
            _db.Set<AuthorBook>().Add(new AuthorBook
            {
                AuthorId = author.AuthorId,
                Isbn = book.Isbn
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var created = await _db.Books
            .Include(b => b.Category)
            .Include(b => b.Publisher)
            .Include(b => b.AuthorBooks)
                .ThenInclude(ab => ab.Author)
            .FirstAsync(b => b.Isbn == book.Isbn, cancellationToken);

        var dto = MapBookToDto(created);
        dto.AiSummary = request.Description;

        return new ApiResponse<AdminAiImportBookResponse>
        {
            Success = true,
            Message = "Tạo sách mới thành công",
            Data = new AdminAiImportBookResponse
            {
                Book = dto
            }
        };
    }


    private async Task<(object Snapshot, HashSet<string> DataSources)> BuildAdminDataSnapshotAsync(
        DateTime from,
        DateTime to,
        bool includeInventorySnapshot,
        bool includeCategoryShare,
        CancellationToken cancellationToken)
    {
        var dataSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ProfitReportDto? profitReport = null;
        var profitResult = await _reportService.GetProfitReportAsync(from, to);
        if (profitResult.Success && profitResult.Data != null)
        {
            profitReport = profitResult.Data;
            dataSources.Add("profit_report");
        }
        else
        {
            _logger.LogWarning("AdminChat: Không thể lấy báo cáo lợi nhuận: {Message}", profitResult.Message);
        }

        RevenueReportResponse? revenueReport = null;
        var revenueResult = await _reportService.GetRevenueByDateRangeAsync(new RevenueReportRequest
        {
            FromDate = from.Date,
            ToDate = to.Date
        });
        if (revenueResult.Success && revenueResult.Data != null)
        {
            revenueReport = revenueResult.Data;
            dataSources.Add("revenue_daily");
        }
        else
        {
            _logger.LogWarning("AdminChat: Không thể lấy báo cáo doanh thu ngày: {Message}", revenueResult.Message);
        }

        InventoryReportResponse? inventorySnapshot = null;
        if (includeInventorySnapshot)
        {
            var inventoryResult = await _reportService.GetInventoryAsOfDateAsync(to.Date);
            if (inventoryResult.Success && inventoryResult.Data != null)
            {
                inventorySnapshot = inventoryResult.Data;
                dataSources.Add("inventory_snapshot");
            }
            else
            {
                _logger.LogWarning("AdminChat: Không thể lấy báo cáo tồn kho: {Message}", inventoryResult.Message);
            }
        }

        BooksByCategoryResponse? categoryShare = null;
        if (includeCategoryShare)
        {
            var categoryResult = await _reportService.GetBooksByCategoryAsync();
            if (categoryResult.Success && categoryResult.Data != null)
            {
                categoryShare = categoryResult.Data;
                dataSources.Add("category_share");
            }
            else
            {
                _logger.LogWarning("AdminChat: Không thể lấy tỷ trọng danh mục: {Message}", categoryResult.Message);
            }
        }

        var profitPayload = profitReport == null
            ? null
            : new
            {
                summary = new
                {
                    profitReport.OrdersCount,
                    profitReport.Revenue,
                    profitReport.CostOfGoods,
                    profitReport.OperatingExpenses,
                    profit = profitReport.Profit
                },
                topSoldItems = profitReport.TopSoldItems
                    .OrderByDescending(i => i.QtySold)
                    .Take(10)
                    .Select(i => new
                    {
                        i.Isbn,
                        i.Title,
                        i.QtySold,
                        i.Revenue,
                        i.Profit
                    })
                    .ToList(),
                topMarginItems = profitReport.TopMarginItems
                    .OrderByDescending(i => i.MarginPct)
                    .Take(10)
                    .Select(i => new
                    {
                        i.Isbn,
                        i.Title,
                        i.MarginPct,
                        i.Revenue,
                        i.Profit
                    })
                    .ToList()
            };

        var revenuePayload = revenueReport == null
            ? null
            : new
            {
                totalRevenue = revenueReport.TotalRevenue,
                last30Days = revenueReport.Items
                    .OrderBy(i => i.Day)
                    .TakeLast(30)
                    .Select(i => new
                    {
                        day = i.Day,
                        i.Revenue
                    })
                    .ToList()
            };

        var inventoryPayload = inventorySnapshot == null
            ? null
            : new
            {
                date = inventorySnapshot.ReportDate,
                topSkus = inventorySnapshot.Items
                    .OrderByDescending(i => i.QuantityOnHand)
                    .ThenByDescending(i => i.AveragePrice)
                    .Take(20)
                    .Select(i => new
                    {
                        i.Isbn,
                        i.Title,
                        i.Category,
                        quantityOnHand = i.QuantityOnHand,
                        averagePrice = i.AveragePrice,
                        inventoryValue = Math.Round(i.AveragePrice * i.QuantityOnHand, 2)
                    })
                    .ToList()
            };

        var categoryPayload = categoryShare == null
            ? null
            : new
            {
                categoryShare.Total,
                items = categoryShare.Items
                    .OrderByDescending(i => i.Percent)
                    .Take(10)
                    .Select(i => new
                    {
                        i.Category,
                        i.Count,
                        i.Percent
                    })
                    .ToList()
            };

        var snapshot = new
        {
            timeframe = new
            {
                fromUtc = from,
                toUtc = to,
                days = Math.Round((to - from).TotalDays, 2)
            },
            profitReport = profitPayload,
            revenueReport = revenuePayload,
            inventorySnapshot = inventoryPayload,
            categoryShare = categoryPayload
        };

        return (snapshot, dataSources);
    }

    private static IReadOnlyList<object> GetAdminTableSchemas()
        => AdminTableSchemas;

    private async Task EnrichBookSuggestionsAsync(
        List<AdminAiBookSuggestion> suggestions,
        CancellationToken cancellationToken)
    {
        if (suggestions == null || suggestions.Count == 0)
        {
            return;
        }

        var degree = Math.Max(2, Math.Min(8, Environment.ProcessorCount * 2));
        using var semaphore = new SemaphoreSlim(degree);
        var tasks = suggestions.Select(s => EnrichSingleSuggestionAsync(s, semaphore, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task EnrichSingleSuggestionAsync(
        AdminAiBookSuggestion suggestion,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        if (suggestion == null || string.IsNullOrWhiteSpace(suggestion.Title))
        {
            return;
        }

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var enrichment = await FetchBookMetadataAsync(suggestion.Title, cancellationToken);
            if (enrichment == null)
            {
                suggestion.SuggestedIsbn ??= await GenerateUniqueIsbnAsync(null, cancellationToken);
                return;
            }

            suggestion.Description ??= enrichment.Description;
            suggestion.AuthorName ??= enrichment.AuthorName;
            suggestion.PublisherName ??= enrichment.PublisherName;
            suggestion.PageCount ??= enrichment.PageCount;
            suggestion.SuggestedPrice ??= enrichment.PriceVnd ?? TryParseVndPrice(enrichment.PriceDisplay);
            suggestion.MarketPrice ??= enrichment.PriceDisplay;
            suggestion.MarketSourceName ??= enrichment.SourceName;
            suggestion.MarketSourceUrl ??= enrichment.SourceUrl;
            suggestion.Category ??= enrichment.CategoryName;
            suggestion.PublishYear ??= enrichment.PublishYear;
            suggestion.SuggestedIsbn = await GenerateUniqueIsbnAsync(enrichment.Isbn, cancellationToken);
            suggestion.SuggestedStock ??= 0;

            if (string.IsNullOrWhiteSpace(suggestion.SuggestedCategoryId) && !string.IsNullOrWhiteSpace(enrichment.CategoryName))
            {
                suggestion.SuggestedCategoryId = await ResolveCategoryIdByNameAsync(enrichment.CategoryName, cancellationToken);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static int? TryGetIntProperty(JsonElement json, string propertyName)
    {
        if (!json.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var number))
        {
            return number;
        }

        if (prop.ValueKind == JsonValueKind.String &&
            int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static decimal? TryGetDecimalProperty(JsonElement json, string propertyName)
    {
        if (!json.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetDecimal();
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            var str = prop.GetString();
            if (decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return TryParseVndPrice(str);
        }

        return null;
    }

    private static string StripCodeFence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var newlineIndex = trimmed.IndexOf('\n');
            if (newlineIndex < 0)
            {
                newlineIndex = trimmed.IndexOf('\r');
            }

            if (newlineIndex >= 0 && newlineIndex + 1 < trimmed.Length)
            {
                trimmed = trimmed[(newlineIndex + 1)..];
            }

            trimmed = trimmed.TrimStart('\r', '\n');

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }
        }

        return trimmed.Trim();
    }

    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var startIndex = trimmed.IndexOf('{');
        var endIndex = trimmed.LastIndexOf('}');
        if (startIndex >= 0 && endIndex > startIndex)
        {
            return trimmed.Substring(startIndex, endIndex - startIndex + 1);
        }

        return null;
    }

    private static BookSuggestionEnrichment? TryExtractMetadataFromPlainText(string text, string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string? priceDisplay = null;
        decimal? priceVnd = null;
        var priceMatch = Regex.Match(text, @"(?<amount>\d{1,3}(?:[.,]\d{3})+|\d+)\s?(?:vnd|vnđ|đ|₫)", RegexOptions.IgnoreCase);
        if (priceMatch.Success)
        {
            priceDisplay = priceMatch.Value.Trim();
            var amountDigits = Regex.Replace(priceMatch.Groups["amount"].Value, @"[^\d]", string.Empty);
            if (decimal.TryParse(amountDigits, out var parsed))
            {
                priceVnd = parsed;
            }
        }

        string? sourceUrl = null;
        var urlMatch = Regex.Match(text, @"https?:\/\/\S+", RegexOptions.IgnoreCase);
        if (urlMatch.Success)
        {
            sourceUrl = urlMatch.Value.TrimEnd('.', ',', ';');
        }

        string? sourceName = null;
        var knownSources = new[]
        {
            "Shopee","Lazada","Tiki","Fahasa","Phương Nam","Phuong Nam","NewShop","Vinabook"
        };
        foreach (var source in knownSources)
        {
            if (text.Contains(source, StringComparison.OrdinalIgnoreCase))
            {
                sourceName = source;
                break;
            }
        }

        if (priceDisplay == null && sourceName == null && sourceUrl == null)
        {
            return null;
        }

        return new BookSuggestionEnrichment(
            Title: fallbackTitle,
            Description: text.Trim(),
            AuthorName: null,
            PublisherName: null,
            PageCount: null,
            PriceVnd: priceVnd,
            PriceDisplay: priceDisplay,
            CategoryName: null,
            PublishYear: null,
            Isbn: null,
            SourceName: sourceName,
            SourceUrl: sourceUrl);
    }

    private static decimal? TryParseVndPrice(string? priceText)
    {
        if (string.IsNullOrWhiteSpace(priceText))
        {
            return null;
        }

        var digits = new string(priceText.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits))
        {
            return null;
        }

        if (decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static ChatAnswerFormatting BuildChatAnswerFormatting(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new ChatAnswerFormatting(string.Empty, string.Empty);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawText);
            var markdown = BuildMarkdownFromStructuredAnswer(doc.RootElement);
            if (string.IsNullOrWhiteSpace(markdown))
            {
                markdown = rawText.Trim();
            }

            var plain = MarkdownToPlainText(markdown);
            return new ChatAnswerFormatting(plain, markdown);
        }
        catch
        {
            var fallback = rawText.Trim();
            return new ChatAnswerFormatting(MarkdownToPlainText(fallback), fallback);
        }
    }

    private static string BuildMarkdownFromStructuredAnswer(JsonElement root)
    {
        var sb = new StringBuilder();

        void AppendSection(string title, string? content)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                sb.Append("**").Append(title).Append(":** ").AppendLine(content.Trim());
                sb.AppendLine();
            }
        }

        void AppendList(string title, JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
            {
                return;
            }

            sb.Append("**").Append(title).Append(":**").AppendLine();
            foreach (var child in element.EnumerateArray())
            {
                var text = child.ValueKind == JsonValueKind.String
                    ? child.GetString()
                    : child.ValueKind == JsonValueKind.Object && child.TryGetProperty("text", out var val) && val.ValueKind == JsonValueKind.String
                        ? val.GetString()
                        : null;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append("- ").AppendLine(text.Trim());
                }
            }

            sb.AppendLine();
        }

        if (root.TryGetProperty("overview", out var overview) && overview.ValueKind == JsonValueKind.String)
        {
            AppendSection("Tóm tắt", overview.GetString());
        }

        if (root.TryGetProperty("tablesUsed", out var tablesUsed) && tablesUsed.ValueKind == JsonValueKind.Array)
        {
            var hasAny = false;
            var builder = new StringBuilder();
            foreach (var entry in tablesUsed.EnumerateArray())
            {
                if (!entry.TryGetProperty("table", out var tableProp) || tableProp.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var tableName = tableProp.GetString();
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    continue;
                }

                hasAny = true;
                var columns = entry.TryGetProperty("columns", out var colsProp) && colsProp.ValueKind == JsonValueKind.Array
                    ? colsProp.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!.Trim())
                        .ToArray()
                    : Array.Empty<string>();
                var reason = entry.TryGetProperty("reason", out var reasonProp) && reasonProp.ValueKind == JsonValueKind.String
                    ? reasonProp.GetString()
                    : null;

                builder.Append("- ").Append(tableName.Trim());
                if (columns.Length > 0)
                {
                    builder.Append(" (").Append(string.Join(", ", columns)).Append(')');
                }

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    builder.Append(": ").Append(reason.Trim());
                }

                builder.AppendLine();
            }

            if (hasAny)
            {
                sb.Append("**Bảng liên quan:**").AppendLine();
                sb.Append(builder.ToString()).AppendLine();
            }
        }

        if (root.TryGetProperty("metrics", out var metrics))
        {
            AppendList("Chỉ số chính", metrics);
        }

        if (root.TryGetProperty("insights", out var insights))
        {
            AppendList("Nhận định", insights);
        }

        if (root.TryGetProperty("recommendedActions", out var actions))
        {
            AppendList("Hành động đề xuất", actions);
        }

        if (root.TryGetProperty("sqlExamples", out var sqlExamples) && sqlExamples.ValueKind == JsonValueKind.Array)
        {
            var contentBuilder = new StringBuilder();
            foreach (var entry in sqlExamples.EnumerateArray())
            {
                var title = entry.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                    ? titleProp.GetString()
                    : null;
                var statement = entry.TryGetProperty("statement", out var stmtProp) && stmtProp.ValueKind == JsonValueKind.String
                    ? stmtProp.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(statement))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(title))
                {
                    contentBuilder.Append("- ").AppendLine(title.Trim());
                }
                contentBuilder.Append("```sql").AppendLine();
                contentBuilder.Append(statement.Trim()).AppendLine();
                contentBuilder.Append("```").AppendLine();
            }

            if (contentBuilder.Length > 0)
            {
                sb.Append("**Truy vấn gợi ý:**").AppendLine();
                sb.Append(contentBuilder.ToString()).AppendLine();
            }
        }

        if (root.TryGetProperty("mentionDataSources", out var mentionData) && mentionData.ValueKind == JsonValueKind.Array)
        {
            var sources = mentionData
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .ToArray();

            if (sources.Length > 0)
            {
                sb.Append("Nguồn dữ liệu: ").AppendLine(string.Join(", ", sources));
            }
        }

        return sb.ToString().Trim();
    }

    private static string MarkdownToPlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var text = markdown.Replace("\r\n", "\n");
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1", RegexOptions.Singleline);
        text = Regex.Replace(text, @"`{1,3}", string.Empty);
        text = Regex.Replace(text, @"!\[.*?\]\(.*?\)", string.Empty);
        text = Regex.Replace(text, @"\[(.*?)\]\(.*?\)", "$1");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private sealed record ChatAnswerFormatting(string PlainText, string Markdown);

    private sealed record AdminAiAnswerResult(string PlainText, string Markdown, List<string> DataSources);

    private sealed record AiSqlPlan(string Summary, IReadOnlyList<AiSqlPlanStep> Steps);

    private sealed record AiSqlPlanStep(string Alias, string Description, string Sql);

    private sealed record AiSqlResult(string Alias, string Description, List<Dictionary<string, object?>> Rows);

    private bool ValidateAiSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var normalized = sql.Trim();
        if (!(normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
              normalized.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (normalized.IndexOf(';') >= 0)
        {
            return false;
        }

        string[] forbidden = { "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE", "MERGE" };
        return forbidden.All(token => normalized.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0);
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteSqlQueryAsync(
        string sql,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            shouldClose = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 30;

            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var fieldNames = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToArray();

            var count = 0;
            while (await reader.ReadAsync(cancellationToken) && count < maxRows)
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in fieldNames)
                {
                    var value = reader[name];
                    dict[name] = value == DBNull.Value ? null : value;
                }

                rows.Add(dict);
                count++;
            }

            return rows;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static (string? AudioBase64, string? MimeType) ExtractAudioFromResponse(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("inlineData", out var inlineData) && inlineData.ValueKind == JsonValueKind.Object)
                {
                    var data = inlineData.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.String
                        ? dataProp.GetString()
                        : null;
                    var mime = inlineData.TryGetProperty("mimeType", out var mimeProp) && mimeProp.ValueKind == JsonValueKind.String
                        ? mimeProp.GetString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        return (data, mime);
                    }
                }
            }
        }

        return (null, null);
    }

    private async Task<BookSuggestionEnrichment?> FetchBookMetadataAsync(string title, CancellationToken cancellationToken)
    {
        var systemPrompt = @"Bạn là trợ lý nhập hàng cho nhà sách.
Nhiệm vụ: tìm thông tin đầy đủ về cuốn sách mà admin đang cân nhắc nhập thêm.
Sử dụng Google Search để lấy dữ liệu mới nhất (mô tả nội dung, tác giả, NXB, số trang, năm XB, giá bán, ISBN).
Trả về duy nhất JSON:
{
  ""title"": ""..."",
  ""description"": ""..."",
  ""authors"": [""..."", ""...""],
  ""publisher"": ""..."",
  ""publishYear"": 2024,
  ""pageCount"": 320,
  ""priceVnd"": 145000,
  ""priceDisplay"": ""145.000₫ tại Tiki"",
  ""category"": ""Sách kỹ năng"",
  ""isbn"": ""9786041234567"",
  ""sourceName"": ""Tiki"",
  ""sourceUrl"": ""https://..."" 
}";

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = JsonSerializer.Serialize(new { title }) }
                    }
                }
            },
            tools = new object[]
            {
                new { googleSearch = new { } }
            },
            generationConfig = new
            {
                temperature = 0.3,
                candidateCount = 1
            }
        };

        var doc = await _geminiClient.CallGeminiCustomAsync(payload, null, null, null, cancellationToken);
        if (doc == null)
        {
            return null;
        }

        using (doc)
        {
            var text = _geminiClient.ExtractFirstTextFromResponse(doc);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = StripCodeFence(text);
            var jsonCandidate = ExtractJsonObject(text);
            if (jsonCandidate == null)
            {
                var fallback = TryExtractMetadataFromPlainText(text, title);
                if (fallback != null)
                {
                    return fallback;
                }

                return null;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(jsonCandidate);
                var root = jsonDoc.RootElement;

                var description = root.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                    ? descProp.GetString()
                    : null;
                var publisher = root.TryGetProperty("publisher", out var pubProp) && pubProp.ValueKind == JsonValueKind.String
                    ? pubProp.GetString()
                    : null;
                var pageCount = root.TryGetProperty("pageCount", out var pageProp) && pageProp.ValueKind == JsonValueKind.Number
                    ? pageProp.GetInt32()
                    : (int?)null;
                var priceVnd = root.TryGetProperty("priceVnd", out var priceProp) && priceProp.ValueKind == JsonValueKind.Number
                    ? priceProp.GetDecimal()
                    : (decimal?)null;
                var priceDisplay = root.TryGetProperty("priceDisplay", out var priceDisplayProp) && priceDisplayProp.ValueKind == JsonValueKind.String
                    ? priceDisplayProp.GetString()
                    : null;
                var publishYear = root.TryGetProperty("publishYear", out var publishYearProp) && publishYearProp.ValueKind == JsonValueKind.Number
                    ? publishYearProp.GetInt32()
                    : (int?)null;
                var category = root.TryGetProperty("category", out var categoryProp) && categoryProp.ValueKind == JsonValueKind.String
                    ? categoryProp.GetString()
                    : null;
                var isbn = root.TryGetProperty("isbn", out var isbnProp) && isbnProp.ValueKind == JsonValueKind.String
                    ? isbnProp.GetString()
                    : null;
                var sourceName = root.TryGetProperty("sourceName", out var sourceNameProp) && sourceNameProp.ValueKind == JsonValueKind.String
                    ? sourceNameProp.GetString()
                    : null;
                var sourceUrl = root.TryGetProperty("sourceUrl", out var sourceUrlProp) && sourceUrlProp.ValueKind == JsonValueKind.String
                    ? sourceUrlProp.GetString()
                    : null;

                string? authorName = null;
                if (root.TryGetProperty("authors", out var authorsProp) && authorsProp.ValueKind == JsonValueKind.Array)
                {
                    var names = authorsProp
                        .EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!.Trim())
                        .ToArray();
                    if (names.Length > 0)
                    {
                        authorName = string.Join(", ", names);
                    }
                }

                return new BookSuggestionEnrichment(
                    Title: root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String ? titleProp.GetString() ?? title : title,
                    Description: description,
                    AuthorName: authorName,
                    PublisherName: publisher,
                    PageCount: pageCount,
                    PriceVnd: priceVnd,
                    PriceDisplay: priceDisplay,
                    CategoryName: category,
                    PublishYear: publishYear,
                    Isbn: isbn,
                    SourceName: sourceName,
                    SourceUrl: sourceUrl);
            }
            catch (Exception ex)
            {
                var fallback = TryExtractMetadataFromPlainText(text, title);
                if (fallback != null)
                {
                    _logger.LogWarning(ex, "Failed to parse JSON; using fallback extraction. Raw: {Text}", text);
                    return fallback;
                }

                _logger.LogWarning(ex, "Failed to parse book metadata JSON. Raw: {Text}", text);
                return null;
            }
        }
    }

    private async Task<string> GenerateUniqueIsbnAsync(string? preferredIsbn, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(preferredIsbn))
        {
            var normalized = NormalizeIsbn(preferredIsbn);
            var exists = await _db.Books.AnyAsync(b => b.Isbn == normalized, cancellationToken);
            if (!exists)
            {
                return normalized;
            }
        }

        for (var i = 0; i < 25; i++)
        {
            var candidate = $"978{RandomNumberGenerator.GetInt32(100000000, 999999999)}";
            var exists = await _db.Books.AnyAsync(b => b.Isbn == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N")[..13];
    }

    private static string NormalizeIsbn(string isbn)
        => new string(isbn.Where(char.IsLetterOrDigit).ToArray());

    private async Task<string?> ResolveCategoryIdByNameAsync(string? categoryName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        var category = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == categoryName, cancellationToken);

        return category?.CategoryId.ToString();
    }

    private async Task<long?> ResolveOrCreateCategoryAsync(long? categoryId, string? categoryName, CancellationToken cancellationToken)
    {
        if (categoryId.HasValue)
        {
            var exists = await _db.Categories.AsNoTracking().AnyAsync(c => c.CategoryId == categoryId.Value, cancellationToken);
            if (exists)
            {
                return categoryId.Value;
            }
        }

        var normalizedName = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var existing = await _db.Categories.FirstOrDefaultAsync(c => c.Name == normalizedName, cancellationToken);
        if (existing != null)
        {
            return existing.CategoryId;
        }

        var category = new Category
        {
            Name = normalizedName,
            Description = "Tạo bởi AI"
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);
        return category.CategoryId;
    }

    private async Task<long?> ResolveOrCreatePublisherAsync(long? publisherId, string? publisherName, CancellationToken cancellationToken)
    {
        if (publisherId.HasValue)
        {
            var exists = await _db.Publishers.AsNoTracking().AnyAsync(p => p.PublisherId == publisherId.Value, cancellationToken);
            if (exists)
            {
                return publisherId.Value;
            }
        }

        var normalizedName = string.IsNullOrWhiteSpace(publisherName) ? null : publisherName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var existing = await _db.Publishers.FirstOrDefaultAsync(p => p.Name == normalizedName, cancellationToken);
        if (existing != null)
        {
            return existing.PublisherId;
        }

        var publisher = new Publisher
        {
            Name = normalizedName
        };
        _db.Publishers.Add(publisher);
        await _db.SaveChangesAsync(cancellationToken);
        return publisher.PublisherId;
    }

    private async Task<Author?> ResolveOrCreateAuthorAsync(string? authorName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorName))
        {
            return null;
        }

        var (firstName, lastName) = SplitAuthorName(authorName);
        var existing = await _db.Authors.FirstOrDefaultAsync(
            a => a.FirstName == firstName && a.LastName == lastName,
            cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var author = new Author
        {
            FirstName = firstName,
            LastName = lastName,
            Gender = Gender.Other
        };

        _db.Authors.Add(author);
        await _db.SaveChangesAsync(cancellationToken);
        return author;
    }

    private static (string FirstName, string LastName) SplitAuthorName(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return ("AI", "Author");
        }

        if (parts.Length == 1)
        {
            return (parts[0], parts[0]);
        }

        var firstName = parts[0];
        var lastName = string.Join(' ', parts.Skip(1));
        return (firstName, lastName);
    }

    private async Task<Dictionary<string, MarketPriceInfo>> FetchMarketPriceInsightsAsync(
        IEnumerable<string?> titles,
        CancellationToken cancellationToken)
    {
        var list = titles?
            .Select(t => t?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList() ?? new List<string>();

        if (list.Count == 0)
        {
            return new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var systemPrompt = @"Bạn là trợ lý AI chuyên săn giá sách tại Việt Nam.
Hãy dùng Google Search để tìm giá hiện tại cho danh sách sách được cung cấp.
Trả về đúng JSON theo cấu trúc:
{
  ""items"": [
    {
      ""title"": ""Tên sách"",
      ""marketPrice"": ""Giá tham khảo (khoảng giá hoặc giá tốt nhất, kèm đơn vị VND)"",
      ""sourceName"": ""Tên sàn/web"",
      ""sourceUrl"": ""URL chi tiết""
    }
  ]
}";

        var userPayload = new
        {
            type = "market_price_lookup",
            language = "vi",
            books = list
        };

        var body = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = JsonSerializer.Serialize(userPayload) }
                    }
                }
            },
            tools = new object[]
            {
                new { googleSearch = new { } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                candidateCount = 1
            }
        };

        var doc = await _geminiClient.CallGeminiCustomAsync(body, null, null, null, cancellationToken);
        if (doc == null)
        {
            return new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
        }

        using (doc)
        {
            var text = _geminiClient.ExtractFirstTextFromResponse(doc);
            // Log raw response text for debugging market-price lookups
            try
            {
                _logger.LogInformation("MarketPriceLookup - raw text: {Text}", text ?? string.Empty);
            }
            catch { }
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
            }

            text = StripCodeFence(text);

            try
            {
                using var jsonDoc = JsonDocument.Parse(text);
                var root = jsonDoc.RootElement;
                if (!root.TryGetProperty("items", out var itemsElem) || itemsElem.ValueKind != JsonValueKind.Array)
                {
                    return new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
                }

                var result = new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in itemsElem.EnumerateArray())
                {
                    if (!item.TryGetProperty("title", out var titleProp) || titleProp.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var title = titleProp.GetString();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var price = item.TryGetProperty("marketPrice", out var priceProp) && priceProp.ValueKind == JsonValueKind.String
                        ? priceProp.GetString()
                        : null;
                    var sourceName = item.TryGetProperty("sourceName", out var sourceNameProp) && sourceNameProp.ValueKind == JsonValueKind.String
                        ? sourceNameProp.GetString()
                        : null;
                    var sourceUrl = item.TryGetProperty("sourceUrl", out var sourceUrlProp) && sourceUrlProp.ValueKind == JsonValueKind.String
                        ? sourceUrlProp.GetString()
                        : null;

                    result[NormalizeTitleKey(title)] = new MarketPriceInfo(
                        title,
                        price,
                        sourceName,
                        sourceUrl);
                }

                // Log parsed result keys for easier debugging
                try
                {
                    _logger.LogInformation("MarketPriceLookup - parsed {Count} items: {Keys}", result.Count, string.Join(',', result.Keys));
                }
                catch { }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse market price JSON payload. Raw text: {Text}", text);
                return new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private sealed record AiDocumentSeed(string RefType, string RefId, string Content);
    private sealed record DeliveredOrderLineSnapshot(string Isbn, string? BookTitle, string? CategoryName, int Quantity, decimal UnitPrice, DateTime PlacedAt);
    private sealed record OrderSummaryRow(long OrderId, long CustomerId, DateTime PlacedAt, OrderStatus Status, int Items, decimal Revenue);
    private sealed record CustomerAggregate(long CustomerId, int TotalOrders, int DeliveredOrders, int TotalItems, decimal TotalSpent, DateTime LastOrderAt);

    private static string NormalizeTitleKey(string? title)
        => string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : title.Trim().ToLowerInvariant();


    private sealed record MarketPriceInfo(string Title, string? MarketPrice, string? SourceName, string? SourceUrl);

    // Public wrapper to expose market price lookup via IAiService
    public async Task<ApiResponse<MarketPriceLookupResponse>> GetMarketPriceInsightsAsync(
        MarketPriceLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var titles = request?.Titles ?? new List<string>();
        var map = await FetchMarketPriceInsightsAsync(titles, cancellationToken);

        var items = map.Values
            .Select(m => new MarketPriceItemDto
            {
                Title = m.Title ?? string.Empty,
                MarketPrice = m.MarketPrice,
                SourceName = m.SourceName,
                SourceUrl = m.SourceUrl
            })
            .ToList();

        return new ApiResponse<MarketPriceLookupResponse>
        {
            Success = true,
            Message = "OK",
            Data = new MarketPriceLookupResponse { Items = items }
        };
    }

    private sealed record BookSuggestionEnrichment(
        string Title,
        string? Description,
        string? AuthorName,
        string? PublisherName,
        int? PageCount,
        int? PublishYear,
        decimal? PriceVnd,
        string? PriceDisplay,
        string? CategoryName,
        string? Isbn,
        string? SourceName,
        string? SourceUrl);

    private static BookDto MapBookToDto(Book b)
    {
        return new BookDto
        {
            Isbn = b.Isbn,
            Title = b.Title,
            PageCount = b.PageCount,
            AveragePrice = b.AveragePrice,
            CurrentPrice = b.AveragePrice,
            DiscountedPrice = null,
            PublishYear = b.PublishYear,
            CategoryId = b.CategoryId,
            CategoryName = b.Category?.Name ?? string.Empty,
            PublisherId = b.PublisherId,
            PublisherName = b.Publisher?.Name ?? string.Empty,
            ImageUrl = b.ImageUrl,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
            Stock = b.Stock,
            Status = b.Status,
            Authors = b.AuthorBooks.Select(ab => new AuthorDto
            {
                AuthorId = ab.Author.AuthorId,
                FirstName = ab.Author.FirstName,
                LastName = ab.Author.LastName,
                FullName = ab.Author.FirstName + " " + ab.Author.LastName,
                Gender = ab.Author.Gender,
                DateOfBirth = ab.Author.DateOfBirth
            }).ToList(),
            HasPromotion = false,
            ActivePromotions = new List<BookPromotionDto>()
        };
    }
}



