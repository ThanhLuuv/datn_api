using System.Text;
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
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiService> _logger;

    private const string DefaultModel = "gemini-2.5-flash";

    public AiService(
        BookStoreDbContext db,
        IReportService reportService,
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<AiService> logger)
    {
        _db = db;
        _reportService = reportService;
        _configuration = configuration;
        _httpClient = httpClient;
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

        var aiResultJson = await CallGeminiAsync(
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
            using var doc = JsonDocument.Parse(aiResultJson);
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
        var from = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = request.ToDate ?? DateTime.UtcNow;

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

        // 3) Chuẩn bị payload cho AI
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
            topSoldItems = profitReport.Data.TopSoldItems.Select(i => new
            {
                i.Isbn,
                i.Title,
                i.QtySold,
                i.Revenue,
                i.Cogs,
                i.Profit,
                ratings = ratingStats.TryGetValue(i.Isbn, out var rs)
                    ? new { avgStars = rs.AvgStars, count = rs.Count }
                    : null
            }).ToList(),
            topMarginItems = profitReport.Data.TopMarginItems.Select(i => new
            {
                i.Isbn,
                i.Title,
                i.QtySold,
                i.Revenue,
                i.Cogs,
                i.Profit,
                i.MarginPct,
                ratings = ratingStats.TryGetValue(i.Isbn, out var rs)
                    ? new { avgStars = rs.AvgStars, count = rs.Count }
                    : null
            }).ToList()
        };

        var systemPrompt = @"Bạn là trợ lý phân tích dữ liệu bán hàng cho nhà sách.
Input: danh sách sách bán chạy, lợi nhuận, và thống kê đánh giá khách hàng.
Nhiệm vụ:
- Nhận diện các mặt hàng/bộ sách bán chạy.
- Gợi ý những danh mục (thể loại) nên ưu tiên nhập thêm.
- Gợi ý sách nên:
  + nhập thêm (nếu đã có và có nguy cơ thiếu hàng),
  + hoặc nhập mới (nếu thấy thiếu phân khúc, chủ đề).
- Tổng hợp các nhận xét nổi bật từ đánh giá (ưu/nhược điểm) và đề xuất cải thiện dịch vụ/chất lượng.

TRẢ LỜI DUY NHẤT DƯỚI DẠNG JSON hợp lệ:
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

        var aiResultJson = await CallGeminiAsync(
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
            using var doc = JsonDocument.Parse(aiResultJson);
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
                            : string.Empty
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

        var trimmedHistory = request.Messages
            .TakeLast(12)
            .Select(m => new
            {
                role = (m.Role ?? "user").ToLowerInvariant(),
                content = m.Content?.Trim() ?? string.Empty
            })
            .Where(m => !string.IsNullOrWhiteSpace(m.content))
            .ToList();

        var language = string.IsNullOrWhiteSpace(request.Language)
            ? "vi"
            : request.Language.Trim().ToLowerInvariant();
        var languageLabel = language == "vi" ? "tiếng Việt" : "ngôn ngữ đã yêu cầu";

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
        if (request.IncludeInventorySnapshot)
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
        if (request.IncludeCategoryShare)
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

        var dataPayload = new
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

        var systemPrompt = $@"Bạn là trợ lý dữ liệu thời gian thực cho quản trị viên BookStore.
Bạn được cung cấp dữ liệu JSON (profitReport, revenueReport, inventorySnapshot, categoryShare) và lịch sử hội thoại của admin.
Nhiệm vụ:
- Trả lời câu hỏi dựa trên dữ liệu thực tế, không được đoán.
- Luôn ghi rõ các chỉ số chính (doanh thu, lợi nhuận, tồn kho...) nếu liên quan.
- Đề xuất hành động cụ thể (ví dụ: ""Lọc báo cáo tồn kho ngày hôm nay"", ""Xuất báo cáo doanh thu theo quý"", ...).
- Nếu dữ liệu thiếu, hãy nói rõ và đề xuất bước tiếp theo.
Định dạng câu trả lời: tối đa 4 đoạn/bullet, rõ ràng, bằng {languageLabel}.
";

        var userPayload = new
        {
            question = lastUserMessage.Content,
            conversation = trimmedHistory,
            dataset = dataPayload,
            expectedOutput = new
            {
                language,
                sections = new[]
                {
                    "overview",
                    "metrics",
                    "insights",
                    "recommendedActions"
                },
                mentionDataSources = true
            }
        };

        var aiResultJson = await CallGeminiAsync(
            systemPrompt,
            JsonSerializer.Serialize(userPayload),
            cancellationToken);

        if (aiResultJson == null)
        {
            return new ApiResponse<AdminAiChatResponse>
            {
                Success = false,
                Message = "Không thể kết nối tới dịch vụ AI để trả lời câu hỏi.",
                Errors = new List<string> { "AI service unavailable" }
            };
        }

        var assistantMessage = new AdminAiChatMessage
        {
            Role = "assistant",
            Content = aiResultJson
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
                PlainTextAnswer = aiResultJson,
                MarkdownAnswer = aiResultJson,
                DataSources = dataSources.ToList(),
                Insights = new Dictionary<string, object>
                {
                    ["timeframe"] = new { fromUtc = from, toUtc = to },
                    ["dataSources"] = dataSources.ToList()
                }
            }
        };
    }

    /// <summary>
    /// Gọi Google Gemini (Generative Language API) với system + user prompt. Trả về nội dung text đầu tiên.
    /// </summary>
    private async Task<string?> CallGeminiAsync(string systemPrompt, string userPayload, CancellationToken cancellationToken)
    {
        // Đọc từ biến môi trường (ưu tiên) hoặc từ configuration
        var apiKeyFromEnv = Environment.GetEnvironmentVariable("Gemini__ApiKey");
        var apiKeyFromConfig = _configuration["Gemini:ApiKey"];
        var apiKey = apiKeyFromEnv ?? apiKeyFromConfig;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Gemini:ApiKey is not configured. Env var: {HasEnv}, Config: {HasConfig}. Please set Gemini__ApiKey environment variable.", 
                !string.IsNullOrEmpty(apiKeyFromEnv), !string.IsNullOrEmpty(apiKeyFromConfig));
            return null;
        }

        var model = Environment.GetEnvironmentVariable("Gemini__Model") 
            ?? _configuration["Gemini:Model"] 
            ?? DefaultModel;
        
        var baseUrl = (Environment.GetEnvironmentVariable("Gemini__BaseUrl") 
            ?? _configuration["Gemini:BaseUrl"] 
            ?? "https://generativelanguage.googleapis.com").TrimEnd('/');
        
        _logger.LogDebug("Calling Gemini API: Model={Model}, BaseUrl={BaseUrl}, ApiKeyLength={ApiKeyLength}", 
            model, baseUrl, apiKey?.Length ?? 0);
        
        var url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);

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
                        new { text = userPayload }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                responseMimeType = "application/json"
            }
        };

        requestMessage.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API error {StatusCode}: {Content}", response.StatusCode, content);
                return null;
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var candidateContent))
                {
                    continue;
                }

                if (!candidateContent.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                    {
                        var text = textProp.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
            }

            return null;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "HTTP error calling Gemini API: {Message}", httpEx.Message);
            return null;
        }
        catch (TaskCanceledException timeoutEx)
        {
            _logger.LogError(timeoutEx, "Timeout calling Gemini API");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Gemini API: {Message}", ex.Message);
            return null;
        }
    }

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



