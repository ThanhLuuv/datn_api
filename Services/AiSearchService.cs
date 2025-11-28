using System.Globalization;
using System.Text;
using System.Text.Json;
using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.IO;
using BookStore.Api.Services;

namespace BookStore.Api.Services;

public class AiSearchService : IAiSearchService
{
    private readonly BookStoreDbContext _db;
    private readonly IGeminiClient _geminiClient;
    private readonly IOrderQueryService _orderQueryService;
    private readonly ILogger<AiSearchService> _logger;

    private static readonly JsonSerializerOptions CamelCaseSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // Chỉ index dữ liệu TĨNH (Static Data) - Dữ liệu động sẽ dùng Function Calling
    private static readonly HashSet<string> SupportedKnowledgeRefTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "book",      // Thông tin sách (tĩnh)
        "category"   // Danh mục sách (tĩnh)
    };

    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public AiSearchService(
        BookStoreDbContext db,
        IGeminiClient geminiClient,
        IOrderQueryService orderQueryService,
        ILogger<AiSearchService> logger)
    {
        _db = db;
        _geminiClient = geminiClient;
        _orderQueryService = orderQueryService;
        _logger = logger;
    }

    private const string StoreConfigFileName = "ai_search_store.json";

    public async Task<ApiResponse<AiSearchResponse>> SearchKnowledgeBaseAsync(
        AiSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Query không được để trống",
                Errors = new List<string> { "Query is required" }
            };
        }

        var storeName = await GetActiveStoreNameAsync(cancellationToken);
        if (string.IsNullOrEmpty(storeName))
        {
             return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Hệ thống chưa có dữ liệu tìm kiếm. Vui lòng chạy Reindex trước.",
                Errors = new List<string> { "AI Store not found" }
            };
        }

        var normalizedQuery = request.Query.Trim();
        
        // Construct tool config for File Search
        var toolConfig = new
        {
            file_search = new
            {
                file_search_store_names = new[] { storeName }
            }
        };

        const string systemPrompt = @"Bạn là trợ lý AI chuyên về hệ thống quản lý nhà sách BookStore.
Nhiệm vụ chính: Trả lời các câu hỏi về SÁCH và DANH MỤC dựa trên dữ liệu được cung cấp.

QUY TẮC:
1. Sử dụng công cụ file_search để tìm kiếm thông tin về sách và danh mục trong Knowledge Base.
2. Chỉ trả lời dựa trên thông tin tìm thấy. Nếu không thấy, nói rõ là không có thông tin.
3. Trích dẫn nguồn (ISBN, tên sách, danh mục) nếu có thể.
4. Ưu tiên trả lời tiếng Việt.
5. Cung cấp thông tin chi tiết về sách: tác giả, giá, tồn kho, đánh giá.

CÁCH TRẢ LỜI:
- Ngắn gọn, súc tích.
- Dùng bullet points cho danh sách.
- Nếu hỏi về tồn kho hoặc giá, luôn cung cấp số liệu cụ thể.";

        var answer = await _geminiClient.GenerateContentWithToolAsync(systemPrompt, normalizedQuery, toolConfig, cancellationToken);

        if (string.IsNullOrWhiteSpace(answer))
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Gemini không trả về kết quả (có thể do không tìm thấy thông tin phù hợp).",
                Errors = new List<string> { "No response from Gemini" }
            };
        }

        return new ApiResponse<AiSearchResponse>
        {
            Success = true,
            Message = "OK",
            Data = new AiSearchResponse
            {
                Answer = answer,
                Documents = new List<AiSearchDocumentDto>(), // File Search handles retrieval internally
                Metadata = new Dictionary<string, object?>
                {
                    ["storeName"] = storeName,
                    ["method"] = "Gemini File Search"
                }
            }
        };
    }

    public async Task<ApiResponse<AiSearchReindexResponse>> RebuildAiSearchIndexAsync(
        AiSearchReindexRequest request,
        CancellationToken cancellationToken = default)
    {
        var refTypes = ResolveRequestedRefTypes(request.RefTypes);
        if (refTypes.Count == 0)
        {
            return new ApiResponse<AiSearchReindexResponse>
            {
                Success = false,
                Message = "Không có ref_type hợp lệ để index",
                Errors = new List<string> { "Invalid ref types" }
            };
        }

        _logger.LogInformation("Starting AI Search Reindex (Gemini File Search) - RefTypes: [{RefTypes}]", string.Join(", ", refTypes));

        var seeds = await BuildAiDocumentSeedsAsync(request, refTypes, cancellationToken);
        if (seeds.Count == 0)
        {
             return new ApiResponse<AiSearchReindexResponse>
            {
                Success = false,
                Message = "Không có dữ liệu nào để index",
                Errors = new List<string> { "No data for indexing" }
            };
        }

        // Create a single large text content
        var sb = new StringBuilder();
        foreach (var seed in seeds)
        {
            sb.AppendLine("--- DOCUMENT START ---");
            sb.AppendLine($"RefType: {seed.RefType}");
            sb.AppendLine($"RefId: {seed.RefId}");
            sb.AppendLine(seed.Content);
            sb.AppendLine("--- DOCUMENT END ---");
            sb.AppendLine();
        }

        var content = sb.ToString();
        var fileName = $"bookstore_knowledge_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);

        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);

            // Create Store
            var storeName = await _geminiClient.CreateFileSearchStoreAsync($"BookStore Knowledge {DateTime.UtcNow:yyyy-MM-dd}", cancellationToken);
            if (string.IsNullOrEmpty(storeName))
            {
                 return new ApiResponse<AiSearchReindexResponse>
                {
                    Success = false,
                    Message = "Tạo Store trên Gemini thất bại",
                    Errors = new List<string> { "Create Store failed" }
                };
            }

            // Upload File directly to Store
            using var stream = File.OpenRead(tempPath);
            var fileUri = await _geminiClient.UploadFileToStoreAsync(stream, storeName, "text/plain", cancellationToken);

            if (string.IsNullOrEmpty(fileUri))
            {
                return new ApiResponse<AiSearchReindexResponse>
                {
                    Success = false,
                    Message = "Upload file vào Store thất bại",
                    Errors = new List<string> { "Upload to Store failed" }
                };
            }

            // Save Store Name
            await SaveActiveStoreNameAsync(storeName, cancellationToken);

            // Cleanup old store if possible? (Not implemented here, but good practice)

            return new ApiResponse<AiSearchReindexResponse>
            {
                Success = true,
                Message = "Reindex thành công (Gemini File Search)",
                Data = new AiSearchReindexResponse
                {
                    IndexedDocuments = seeds.Count,
                    IndexedAt = DateTime.UtcNow,
                    RefTypes = refTypes.ToList()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reindex failed");
            return new ApiResponse<AiSearchReindexResponse>
            {
                Success = false,
                Message = "Reindex thất bại: " + ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private async Task<string?> GetActiveStoreNameAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StoreConfigFileName)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(StoreConfigFileName, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("storeName", out var prop))
            {
                return prop.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read store config");
        }
        return null;
    }

    private async Task SaveActiveStoreNameAsync(string storeName, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(new { storeName });
        await File.WriteAllTextAsync(StoreConfigFileName, json, cancellationToken);
    }

    private async Task<List<AiDocumentSeed>> BuildAiDocumentSeedsAsync(
        AiSearchReindexRequest request,
        IReadOnlyList<string> refTypes,
        CancellationToken cancellationToken)
    {
        var seeds = new List<AiDocumentSeed>();
        var maxBooks = Math.Clamp(request.MaxBooks, 50, 2000);

        // ============================================================================
        // STATIC DATA INDEXING ONLY
        // Dynamic data (orders, invoices, customers) will use Function Calling
        // ============================================================================

        if (refTypes.Contains("book"))
        {
            var ratingLookup = await _db.Ratings
                .AsNoTracking()
                .GroupBy(r => r.Isbn)
                .Select(g => new
                {
                    Isbn = g.Key,
                    Count = g.Count(),
                    Avg = g.Average(r => r.Stars)
                })
                .ToDictionaryAsync(x => x.Isbn, x => (x.Count, x.Avg), cancellationToken);

            var books = await _db.Books
                .AsNoTracking()
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.AuthorBooks)
                    .ThenInclude(ab => ab.Author)
                .OrderByDescending(b => b.UpdatedAt)
                .Take(maxBooks)
                .ToListAsync(cancellationToken);

            foreach (var book in books)
            {
                var builder = new StringBuilder();
                var authors = book.AuthorBooks
                    .Select(ab => $"{ab.Author.FirstName} {ab.Author.LastName}".Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                builder.AppendLine("Loại dữ liệu: BOOK");
                builder.AppendLine($"ISBN: {book.Isbn}");
                builder.AppendLine($"Tiêu đề: {book.Title}");
                builder.AppendLine($"Danh mục: {book.Category?.Name}");
                builder.AppendLine($"Nhà xuất bản: {book.Publisher?.Name}");
                builder.AppendLine($"Tác giả: {(authors.Count > 0 ? string.Join(", ", authors) : "Không rõ")}");
                builder.AppendLine($"Năm xuất bản: {book.PublishYear}");
                builder.AppendLine($"Số trang: {book.PageCount}");
                builder.AppendLine($"Giá trung bình: {FormatCurrency(book.AveragePrice)} VNĐ");
                builder.AppendLine($"Tồn kho hiện tại: {book.Stock}");
                builder.AppendLine($"Trạng thái: {(book.Status ? "Đang mở bán" : "Tạm ngưng")}");
                builder.AppendLine($"Ngày cập nhật: {book.UpdatedAt:dd/MM/yyyy HH:mm} UTC");

                if (ratingLookup.TryGetValue(book.Isbn, out var rating))
                {
                    builder.AppendLine($"Đánh giá trung bình: {Math.Round(rating.Item2, 2):0.##}/5 ({rating.Item1} lượt)");
                }

                seeds.Add(new AiDocumentSeed("book", book.Isbn, builder.ToString()));
            }
        }

        if (refTypes.Contains("category"))
        {
            var categories = await _db.Categories
                .AsNoTracking()
                .Include(c => c.Books)
                .ToListAsync(cancellationToken);

            foreach (var category in categories)
            {
                var builder = new StringBuilder();
                var bookCount = category.Books?.Count(b => b.Status) ?? 0;
                var totalStock = category.Books?.Where(b => b.Status).Sum(b => b.Stock) ?? 0;

                builder.AppendLine("Loại dữ liệu: CATEGORY");
                builder.AppendLine($"Category ID: {category.CategoryId}");
                builder.AppendLine($"Tên danh mục: {category.Name}");
                if (!string.IsNullOrWhiteSpace(category.Description))
                {
                    builder.AppendLine($"Mô tả: {category.Description}");
                }
                builder.AppendLine($"Số sách đang bán: {bookCount}");
                builder.AppendLine($"Tổng tồn kho: {totalStock}");

                seeds.Add(new AiDocumentSeed("category", category.CategoryId.ToString(CultureInfo.InvariantCulture), builder.ToString()));
            }
        }

        return seeds;
    }

    private static List<string> ResolveRequestedRefTypes(IEnumerable<string>? requested)
    {
        if (requested == null)
        {
            return SupportedKnowledgeRefTypes.ToList();
        }

        var normalized = requested
            .Select(r => r?.Trim().ToLowerInvariant())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!)
            .Where(r => SupportedKnowledgeRefTypes.Contains(r))
            .Distinct()
            .ToList();

        return normalized.Count == 0 ? SupportedKnowledgeRefTypes.ToList() : normalized;
    }

    private string ExtractAiSearchAnswer(string rawJson, out Dictionary<string, object?>? metadata)
    {
        metadata = null;
        try
        {
            // Try to parse as JSON first
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            
            if (root.ValueKind == JsonValueKind.Object)
            {
                // Try common JSON response formats
                if (root.TryGetProperty("answer", out var answerProp) && answerProp.ValueKind == JsonValueKind.String)
                {
                    var answer = answerProp.GetString() ?? string.Empty;
                    metadata = root.EnumerateObject()
                        .Where(prop => !prop.NameEquals("answer"))
                        .ToDictionary(
                            prop => prop.Name,
                            prop => JsonSerializer.Deserialize<object?>(prop.Value.GetRawText()));
                    return answer;
                }
                
                if (root.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    return textProp.GetString() ?? string.Empty;
                }
                
                if (root.TryGetProperty("content", out var contentProp))
                {
                    if (contentProp.ValueKind == JsonValueKind.String)
                    {
                        return contentProp.GetString() ?? string.Empty;
                    }
                    if (contentProp.ValueKind == JsonValueKind.Object && 
                        contentProp.TryGetProperty("text", out var contentText) && 
                        contentText.ValueKind == JsonValueKind.String)
                    {
                        return contentText.GetString() ?? string.Empty;
                    }
                }
                
                // If it's JSON object but no text field, return as string (fallback)
                metadata = new Dictionary<string, object?>
                {
                    ["rawResponse"] = JsonSerializer.Deserialize<object?>(root.GetRawText())
                };
                return rawJson; // Return original to preserve structure
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString() ?? string.Empty;
            }

            // Fallback: return raw text
            metadata = new Dictionary<string, object?>
            {
                ["rawResponse"] = JsonSerializer.Deserialize<object?>(rawJson) ?? rawJson
            };
            return root.GetRawText();
        }
        catch (Exception ex)
        {
            // If parsing fails, assume it's plain text
            _logger.LogDebug(ex, "AI response is not JSON, treating as plain text");
            metadata = new Dictionary<string, object?>
            {
                ["rawResponse"] = rawJson
            };
            return rawJson;
        }
    }

    private static string TrimContentForPrompt(string content, int maxLength = 3000)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        return content.Length <= maxLength ? content : content[..maxLength];
    }

    private static string GetOrderStatusLabel(OrderStatus status)
        => status switch
        {
            OrderStatus.PendingConfirmation => "Chờ xác nhận",
            OrderStatus.Confirmed => "Đã xác nhận",
            OrderStatus.Delivered => "Đã giao",
            OrderStatus.Cancelled => "Đã huỷ",
            _ => status.ToString()
        };

    private static string GetPaymentStatusLabel(string paymentStatus)
        => paymentStatus?.ToUpperInvariant() switch
        {
            "PENDING" => "Chưa thanh toán",
            "PAID" => "Đã thanh toán",
            "FAILED" => "Thanh toán thất bại",
            "REFUNDED" => "Đã hoàn tiền",
            _ => paymentStatus ?? "Không rõ"
        };

    private static string FormatCurrency(decimal value)
        => string.Format(VietnameseCulture, "{0:0,0}", value);

    private sealed record AiDocumentSeed(string RefType, string RefId, string Content);

    // ============================================================================
    // HYBRID ARCHITECTURE: Function Calling + RAG
    // ============================================================================

    public async Task<ApiResponse<AiSearchResponse>> ChatWithAssistantAsync(
        string userQuery,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Query không được để trống",
                Errors = new List<string> { "Query is required" }
            };
        }

        // Define tools for dynamic data
        var tools = new
        {
            function_declarations = new[]
            {
                new
                {
                    name = "get_order_detail",
                    description = "Lấy thông tin chi tiết, trạng thái và vị trí của đơn hàng theo OrderId.",
                    parameters = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            order_id = new
                            {
                                type = "STRING",
                                description = "Mã đơn hàng hoặc ID đơn hàng (VD: 1024, 55)"
                            }
                        },
                        required = new[] { "order_id" }
                    }
                },
                new
                {
                    name = "search_customer_orders",
                    description = "Tìm kiếm đơn hàng của khách hàng theo tên hoặc ID khách hàng.",
                    parameters = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            customer_identifier = new
                            {
                                type = "STRING",
                                description = "Tên khách hàng hoặc ID khách hàng"
                            }
                        },
                        required = new[] { "customer_identifier" }
                    }
                },
                new
                {
                    name = "get_invoice_detail",
                    description = "Lấy thông tin chi tiết hóa đơn theo InvoiceId hoặc InvoiceNumber.",
                    parameters = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            invoice_id = new
                            {
                                type = "STRING",
                                description = "Mã hóa đơn hoặc số hóa đơn"
                            }
                        },
                        required = new[] { "invoice_id" }
                    }
                },
                new
                {
                    name = "search_books",
                    description = "Tìm kiếm thông tin về sách, tác giả, hoặc nội dung sách trong kho tri thức.",
                    parameters = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            query = new
                            {
                                type = "STRING",
                                description = "Nội dung cần tìm kiếm về sách"
                            }
                        },
                        required = new[] { "query" }
                    }
                }
            }
        };

        const string systemPrompt = $@"Bạn là trợ lý AI của hệ thống quản lý nhà sách BookStore.

NGÀY HIỆN TẠI: {DateTime.Now:dd/MM/yyyy HH:mm} (Múi giờ Việt Nam)

NHIỆM VỤ:
- Trả lời câu hỏi về SÁCH: Dùng công cụ search_books
- Trả lời câu hỏi về ĐƠN HÀNG: Dùng công cụ get_order_detail hoặc search_customer_orders
- Trả lời câu hỏi về HÓA ĐƠN: Dùng công cụ get_invoice_detail

QUY TẮC:
1. LUÔN DÙNG CÔNG CỤ để lấy dữ liệu. ĐỪNG TỰ BỊA thông tin.
2. Nếu user hỏi về đơn hàng, hóa đơn, khách hàng -> GỌI CÔNG CỤ TƯƠNG ỨNG.
3. Nếu user hỏi về sách -> GỌI search_books.
4. Trả lời ngắn gọn, súc tích bằng tiếng Việt.
5. Luôn trích dẫn OrderId, InvoiceId, ISBN khi có.
6. Khi user hỏi về thời gian (hôm qua, tuần trước...), hãy tính toán dựa trên NGÀY HIỆN TẠI ở trên.";

        // Step 1: Call Gemini with tools
        var initialResponse = await _geminiClient.CallGeminiWithToolsAsync(
            systemPrompt, 
            userQuery, 
            tools, 
            cancellationToken);

        if (initialResponse == null)
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Gemini không trả về kết quả",
                Errors = new List<string> { "No response from Gemini" }
            };
        }

        // Step 2: Check if Gemini wants to call a function
        var functionCall = _geminiClient.TryParseFunctionCall(initialResponse);

        if (functionCall != null)
        {
            _logger.LogInformation("Gemini requested function call: {FunctionName} with args: {Args}", 
                functionCall.Name, 
                JsonSerializer.Serialize(functionCall.Args));

            string toolResultJson = "";

            // Step 3: Route to appropriate function
            switch (functionCall.Name)
            {
                case "get_order_detail":
                    if (functionCall.Args.TryGetValue("order_id", out var orderId))
                    {
                        toolResultJson = await _orderQueryService.GetOrderDetailAsync(
                            orderId.ToString() ?? string.Empty, 
                            cancellationToken);
                    }
                    break;

                case "search_customer_orders":
                    if (functionCall.Args.TryGetValue("customer_identifier", out var customerIdentifier))
                    {
                        toolResultJson = await _orderQueryService.SearchCustomerOrdersAsync(
                            customerIdentifier.ToString() ?? string.Empty, 
                            cancellationToken);
                    }
                    break;

                case "get_invoice_detail":
                    if (functionCall.Args.TryGetValue("invoice_id", out var invoiceId))
                    {
                        toolResultJson = await _orderQueryService.GetInvoiceDetailAsync(
                            invoiceId.ToString() ?? string.Empty, 
                            cancellationToken);
                    }
                    break;

                case "search_books":
                    if (functionCall.Args.TryGetValue("query", out var bookQuery))
                    {
                        // Call the existing RAG search for books
                        var bookSearchResult = await SearchKnowledgeBaseAsync(
                            new AiSearchRequest { Query = bookQuery.ToString() ?? string.Empty }, 
                            cancellationToken);
                        
                        toolResultJson = bookSearchResult.Success 
                            ? bookSearchResult.Data?.Answer ?? "Không tìm thấy thông tin" 
                            : "Lỗi khi tìm kiếm sách";
                    }
                    break;

                default:
                    toolResultJson = JsonSerializer.Serialize(new { error = "Unknown function" });
                    break;
            }

            // Log kết quả để debug
            _logger.LogInformation("Function {FunctionName} returned {ResultLength} characters", 
                functionCall.Name, 
                toolResultJson.Length);
            
            if (toolResultJson.Length > 5000)
            {
                _logger.LogWarning("Function result is large ({Length} chars). May hit Gemini context limit.", 
                    toolResultJson.Length);
            }

            // Step 4: Send function result back to Gemini
            var finalAnswer = await _geminiClient.SendFunctionResultAsync(
                systemPrompt,
                userQuery,
                functionCall.Name,
                functionCall.Args, // FIXED: Truyền args gốc từ functionCall
                toolResultJson,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(finalAnswer))
            {
                return new ApiResponse<AiSearchResponse>
                {
                    Success = false,
                    Message = "Gemini không trả về kết quả sau khi xử lý function",
                    Errors = new List<string> { "No final response from Gemini" }
                };
            }

            return new ApiResponse<AiSearchResponse>
            {
                Success = true,
                Message = "OK",
                Data = new AiSearchResponse
                {
                    Answer = finalAnswer,
                    Documents = new List<AiSearchDocumentDto>(),
                    Metadata = new Dictionary<string, object?>
                    {
                        ["method"] = "Function Calling",
                        ["functionCalled"] = functionCall.Name,
                        ["functionArgs"] = functionCall.Args
                    }
                }
            };
        }

        // No function call - extract direct text response
        var textAnswer = _geminiClient.ExtractFirstTextFromResponse(initialResponse);
        
        if (string.IsNullOrWhiteSpace(textAnswer))
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Gemini không trả về text response",
                Errors = new List<string> { "No text in response" }
            };
        }

        return new ApiResponse<AiSearchResponse>
        {
            Success = true,
            Message = "OK",
            Data = new AiSearchResponse
            {
                Answer = textAnswer,
                Documents = new List<AiSearchDocumentDto>(),
                Metadata = new Dictionary<string, object?>
                {
                    ["method"] = "Direct Response"
                }
            }
        };
    }
}
