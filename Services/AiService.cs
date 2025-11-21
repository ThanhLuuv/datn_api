using System.Security.Cryptography;
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
                        PageCount = s.TryGetProperty("pageCount", out var pcProp) && pcProp.ValueKind == JsonValueKind.Number
                            ? pcProp.GetInt32()
                            : (int?)null,
                        PublishYear = s.TryGetProperty("publishYear", out var pyProp) && pyProp.ValueKind == JsonValueKind.Number
                            ? pyProp.GetInt32()
                            : (int?)null,
                        SuggestedPrice = s.TryGetProperty("suggestedPrice", out var spProp) && spProp.ValueKind == JsonValueKind.Number
                            ? spProp.GetDecimal()
                            : (decimal?)null,
                        SuggestedStock = s.TryGetProperty("suggestedStock", out var ssProp) && ssProp.ValueKind == JsonValueKind.Number
                            ? ssProp.GetInt32()
                            : (int?)null,
                        CoverImageUrl = s.TryGetProperty("coverImageUrl", out var ciProp) && ciProp.ValueKind == JsonValueKind.String
                            ? ciProp.GetString()
                            : null,
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
            await EnrichBookSuggestionsAsync(response.BookSuggestions, cancellationToken);

            var priceMap = await FetchMarketPriceInsightsAsync(
                response.BookSuggestions.Select(s => s.Title),
                cancellationToken);

            foreach (var suggestion in response.BookSuggestions)
            {
                var key = NormalizeTitleKey(suggestion.Title);
                if (!string.IsNullOrEmpty(key) && priceMap.TryGetValue(key, out var priceInfo))
                {
                    suggestion.MarketPrice = string.IsNullOrWhiteSpace(priceInfo.MarketPrice)
                        ? suggestion.MarketPrice
                        : priceInfo.MarketPrice;
                    suggestion.MarketSourceName = string.IsNullOrWhiteSpace(priceInfo.SourceName)
                        ? suggestion.MarketSourceName
                        : priceInfo.SourceName;
                    suggestion.MarketSourceUrl = string.IsNullOrWhiteSpace(priceInfo.SourceUrl)
                        ? suggestion.MarketSourceUrl
                        : priceInfo.SourceUrl;
                }

                suggestion.SuggestedIsbn ??= await GenerateUniqueIsbnAsync(null, cancellationToken);
            }
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

        var (dataPayload, dataSources) = await BuildAdminDataSnapshotAsync(
            from,
            to,
            request.IncludeInventorySnapshot,
            request.IncludeCategoryShare,
            cancellationToken);

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

        var (dataPayload, dataSources) = await BuildAdminDataSnapshotAsync(
            from,
            to,
            request.IncludeInventorySnapshot,
            request.IncludeCategoryShare,
            cancellationToken);

        var language = string.IsNullOrWhiteSpace(request.Language)
            ? "vi"
            : request.Language.Trim().ToLowerInvariant();
        var languageLabel = language == "vi" ? "tiếng Việt" : "ngôn ngữ đã yêu cầu";
        var voiceName = Environment.GetEnvironmentVariable("Gemini__Voice")
            ?? _configuration["Gemini:Voice"]
            ?? "Zephyr";
        var mimeType = string.IsNullOrWhiteSpace(request.MimeType)
            ? "audio/webm"
            : request.MimeType!;

        var contextPayload = new
        {
            dataset = dataPayload,
            language,
            expectations = new[]
            {
                "Trả lời dựa trên dữ liệu thật (profitReport, revenueReport, inventorySnapshot, categoryShare).",
                "Nếu dữ liệu thiếu thì nói rõ và đề xuất bước tiếp theo.",
                "Câu trả lời nói tối đa 45 giây, giọng tự tin, chuyên nghiệp."
            }
        };

        var systemPrompt = $@"Bạn là trợ lý dữ liệu giọng nói cho quản trị viên BookStore.
- Phân tích câu hỏi của admin và dùng dữ liệu được cung cấp (dataset JSON).
- Ưu tiên nêu con số chính xác (doanh thu, lợi nhuận, lượng tồn).
- Đề xuất hành động cụ thể sau khi trả lời.
- Giữ câu trả lời ngắn gọn, rõ ràng bằng {languageLabel}.";

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
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = request.AudioBase64
                            }
                        },
                        new
                        {
                            text = JsonSerializer.Serialize(contextPayload)
                        }
                    }
                }
            },
            responseModalities = new[] { "AUDIO" },
            generationConfig = new
            {
                temperature = 0.35,
                candidateCount = 1,
                responseMimeType = "application/json",
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

        var doc = await CallGeminiCustomAsync(body, null, null, null, cancellationToken);
        if (doc == null)
        {
            return new ApiResponse<AdminAiVoiceResponse>
            {
                Success = false,
                Message = "Không thể gọi dịch vụ AI voice.",
                Errors = new List<string> { "AI service unavailable" }
            };
        }

        string? answerText = null;
        string? transcript = null;
        string? audioBase64 = null;
        string? audioMimeType = null;

        using (doc)
        {
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in candidates.EnumerateArray())
                {
                    if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                    {
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                            {
                                answerText ??= textProp.GetString();
                            }

                            if (part.TryGetProperty("inlineData", out var inlineData))
                            {
                                audioBase64 ??= inlineData.TryGetProperty("data", out var dataProp) ? dataProp.GetString() : null;
                                audioMimeType ??= inlineData.TryGetProperty("mimeType", out var mimeProp) ? mimeProp.GetString() : null;
                            }
                        }
                    }

                    if (candidate.TryGetProperty("metadata", out var metadata))
                    {
                        if (metadata.TryGetProperty("outputAudioTranscription", out var transcriptionObj) &&
                            transcriptionObj.TryGetProperty("text", out var transcriptProp) &&
                            transcriptProp.ValueKind == JsonValueKind.String)
                        {
                            transcript = transcriptProp.GetString();
                        }
                    }
                }
            }
        }

        transcript ??= answerText;

        if (string.IsNullOrWhiteSpace(audioBase64))
        {
            _logger.LogWarning("Gemini voice response missing audio data.");
            return new ApiResponse<AdminAiVoiceResponse>
            {
                Success = false,
                Message = "AI không trả về dữ liệu audio.",
                Errors = new List<string> { "missing_audio" }
            };
        }

        return new ApiResponse<AdminAiVoiceResponse>
        {
            Success = true,
            Message = "Voice assistant trả lời thành công",
            Data = new AdminAiVoiceResponse
            {
                Transcript = transcript,
                AnswerText = answerText,
                AudioBase64 = audioBase64,
                AudioMimeType = audioMimeType ?? "audio/wav",
                DataSources = dataSources.ToList()
            }
        };
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
            ImageUrl = request.CoverImageUrl,
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

    private async Task EnrichBookSuggestionsAsync(
        List<AdminAiBookSuggestion> suggestions,
        CancellationToken cancellationToken)
    {
        foreach (var suggestion in suggestions)
        {
            if (string.IsNullOrWhiteSpace(suggestion.Title))
            {
                continue;
            }

            var enrichment = await FetchBookMetadataAsync(suggestion.Title, cancellationToken);
            if (enrichment == null)
            {
                suggestion.SuggestedIsbn ??= await GenerateUniqueIsbnAsync(null, cancellationToken);
                continue;
            }

            suggestion.Description ??= enrichment.Description;
            suggestion.AuthorName ??= enrichment.AuthorName;
            suggestion.PublisherName ??= enrichment.PublisherName;
            suggestion.PageCount ??= enrichment.PageCount;
            suggestion.SuggestedPrice ??= enrichment.PriceVnd;
            suggestion.CoverImageUrl ??= enrichment.ImageUrl;
            suggestion.MarketPrice ??= enrichment.PriceDisplay;
            suggestion.MarketSourceName ??= enrichment.SourceName;
            suggestion.MarketSourceUrl ??= enrichment.SourceUrl;
            suggestion.Category ??= suggestion.Category ?? enrichment.CategoryName;
            suggestion.PublishYear ??= enrichment.PublishYear;
            suggestion.SuggestedIsbn = await GenerateUniqueIsbnAsync(enrichment.Isbn, cancellationToken);
            suggestion.SuggestedStock ??= 0;

            if (string.IsNullOrWhiteSpace(suggestion.SuggestedCategoryId) && !string.IsNullOrWhiteSpace(enrichment.CategoryName))
            {
                suggestion.SuggestedCategoryId = await ResolveCategoryIdByNameAsync(enrichment.CategoryName, cancellationToken);
            }
        }
    }

    private async Task<BookSuggestionEnrichment?> FetchBookMetadataAsync(string title, CancellationToken cancellationToken)
    {
        var systemPrompt = @"Bạn là trợ lý nhập hàng cho nhà sách.
Nhiệm vụ: tìm thông tin đầy đủ về cuốn sách mà admin đang cân nhắc nhập thêm.
Sử dụng Google Search để lấy dữ liệu mới nhất (mô tả nội dung, tác giả, NXB, số trang, năm XB, giá bán, ảnh bìa, ISBN).
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
  ""imageUrl"": ""https://..."",
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
                responseMimeType = "application/json"
            }
        };

        var doc = await CallGeminiCustomAsync(payload, null, null, null, cancellationToken);
        if (doc == null)
        {
            return null;
        }

        using (doc)
        {
            var text = ExtractFirstTextFromResponse(doc);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(text);
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
                var imageUrl = root.TryGetProperty("imageUrl", out var imageProp) && imageProp.ValueKind == JsonValueKind.String
                    ? imageProp.GetString()
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
                    ImageUrl: imageUrl,
                    SourceName: sourceName,
                    SourceUrl: sourceUrl);
            }
            catch (Exception ex)
            {
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
                responseMimeType = "application/json"
            }
        };

        var doc = await CallGeminiCustomAsync(body, null, null, null, cancellationToken);
        if (doc == null)
        {
            return new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
        }

        using (doc)
        {
            var text = ExtractFirstTextFromResponse(doc);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
            }

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

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse market price JSON payload. Raw text: {Text}", text);
                return new Dictionary<string, MarketPriceInfo>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static string NormalizeTitleKey(string? title)
        => string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : title.Trim().ToLowerInvariant();

    private bool TryPrepareGeminiRequest(out string model, out string baseUrl, out string apiKey)
    {
        apiKey = Environment.GetEnvironmentVariable("Gemini__ApiKey")
            ?? _configuration["Gemini:ApiKey"]
            ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var hasEnv = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Gemini__ApiKey"));
            var hasConfig = !string.IsNullOrEmpty(_configuration["Gemini:ApiKey"]);
            _logger.LogWarning("Gemini:ApiKey is not configured. Env var: {HasEnv}, Config: {HasConfig}. Please set Gemini__ApiKey environment variable.", 
                hasEnv, hasConfig);
            model = DefaultModel;
            baseUrl = "https://generativelanguage.googleapis.com";
            return false;
        }

        model = Environment.GetEnvironmentVariable("Gemini__Model")
            ?? _configuration["Gemini:Model"] 
            ?? DefaultModel;
        
        baseUrl = (Environment.GetEnvironmentVariable("Gemini__BaseUrl")
            ?? _configuration["Gemini:BaseUrl"] 
            ?? "https://generativelanguage.googleapis.com").TrimEnd('/');
        
        return true;
    }

    /// <summary>
    /// Gọi Google Gemini (Generative Language API) với system + user prompt. Trả về nội dung text đầu tiên.
    /// </summary>
    private async Task<string?> CallGeminiAsync(string systemPrompt, string userPayload, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out var model, out var baseUrl, out var apiKey))
        {
            return null;
        }

        _logger.LogDebug("Calling Gemini API: Model={Model}, BaseUrl={BaseUrl}, ApiKeyLength={ApiKeyLength}", 
            model, baseUrl, apiKey?.Length ?? 0);
        
        var url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";
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

        var doc = await CallGeminiCustomAsync(body, model, baseUrl, apiKey, cancellationToken);
        if (doc == null)
        {
                return null;
            }

        using var docRef = doc;
        if (!docRef.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                return null;
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

    private async Task<JsonDocument?> CallGeminiCustomAsync(object payload, string? modelOverride, string? baseUrlOverride, string? apiKeyOverride, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out var defaultModel, out var defaultBaseUrl, out var defaultApiKey))
        {
            return null;
        }

        var model = string.IsNullOrWhiteSpace(modelOverride) ? defaultModel : modelOverride!;
        var baseUrl = string.IsNullOrWhiteSpace(baseUrlOverride) ? defaultBaseUrl : baseUrlOverride!.TrimEnd('/');
        var apiKey = string.IsNullOrWhiteSpace(apiKeyOverride) ? defaultApiKey : apiKeyOverride!;

        var url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API error {StatusCode}: {Content}", response.StatusCode, content);
                return null;
            }

            return JsonDocument.Parse(content);
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

    private static string? ExtractFirstTextFromResponse(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            return null;
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

    private sealed record MarketPriceInfo(string Title, string? MarketPrice, string? SourceName, string? SourceUrl);

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
        string? ImageUrl,
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



