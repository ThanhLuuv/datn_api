using System.Globalization;
using System.Text;
using System.Text.Json;
using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookStore.Api.Services;

public class AiSearchService : IAiSearchService
{
    private readonly BookStoreDbContext _db;
    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<AiSearchService> _logger;

    private static readonly JsonSerializerOptions CamelCaseSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly HashSet<string> SupportedKnowledgeRefTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "book",
        "order",
        "order_line",
        "customer",
        "purchase_order",
        "purchase_order_line",
        "goods_receipt",
        "inventory",
        "sales_insight",
        "category"
    };

    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public AiSearchService(
        BookStoreDbContext db,
        IGeminiClient geminiClient,
        ILogger<AiSearchService> logger)
    {
        _db = db;
        _geminiClient = geminiClient;
        _logger = logger;
    }

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

        var normalizedQuery = request.Query.Trim();
        var topK = Math.Clamp(request.TopK, 1, 15);
        var targetRefTypes = ResolveRequestedRefTypes(request.RefTypes);

        var documents = await _db.AiDocuments
            .AsNoTracking()
            .Where(doc => targetRefTypes.Contains(doc.RefType))
            .ToListAsync(cancellationToken);

        if (documents.Count == 0)
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Chưa có dữ liệu trong AI search index. Hãy chạy reindex trước.",
                Errors = new List<string> { "AI index empty" }
            };
        }

        var questionEmbedding = await _geminiClient.GetEmbeddingAsync(normalizedQuery, cancellationToken);
        if (questionEmbedding.Length == 0)
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Không thể tạo embedding cho câu hỏi",
                Errors = new List<string> { "Embedding failed" }
            };
        }

        var scoredDocuments = new List<AiSearchDocumentDto>(documents.Count);
        var similarityThreshold = 0.1; // Giảm từ 0.2 xuống 0.1 để tìm được nhiều documents hơn
        var parsedCount = 0;
        var skippedCount = 0;
        var topSimilarities = new List<double>();

        foreach (var doc in documents)
        {
            if (!TryParseEmbeddingVector(doc.EmbeddingJson, out var embedding) ||
                embedding.Length != questionEmbedding.Length)
            {
                skippedCount++;
                continue;
            }

            parsedCount++;
            var similarity = CosineSimilarity(questionEmbedding, embedding);
            
            // Lưu top 5 similarities để debug (kể cả khi < threshold)
            if (topSimilarities.Count < 5 || similarity > topSimilarities.Min())
            {
                topSimilarities.Add(similarity);
                if (topSimilarities.Count > 5)
                {
                    topSimilarities.Remove(topSimilarities.Min());
                }
            }

            if (double.IsNaN(similarity) || similarity < similarityThreshold)
            {
                continue;
            }

            scoredDocuments.Add(new AiSearchDocumentDto
            {
                Id = doc.Id,
                RefType = doc.RefType,
                RefId = doc.RefId,
                Content = doc.Content,
                UpdatedAt = doc.UpdatedAt,
                Similarity = similarity
            });
        }

        _logger.LogInformation(
            "AI Search - Query: '{Query}', Total docs: {Total}, Parsed: {Parsed}, Skipped: {Skipped}, Matched (>= {Threshold}): {Matched}, Top similarities: [{TopSims}]",
            normalizedQuery,
            documents.Count,
            parsedCount,
            skippedCount,
            similarityThreshold,
            scoredDocuments.Count,
            string.Join(", ", topSimilarities.OrderByDescending(s => s).Select(s => s.ToString("F4"))));

        if (scoredDocuments.Count == 0)
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Không tìm thấy tài liệu phù hợp trong AI index",
                Errors = new List<string> { "No matching documents" }
            };
        }

        var orderedDocs = scoredDocuments
            .OrderByDescending(d => d.Similarity)
            .ThenBy(d => d.RefType)
            .Take(topK)
            .ToList();

        var payload = new
        {
            question = normalizedQuery,
            language = string.IsNullOrWhiteSpace(request.Language)
                ? "vi"
                : request.Language.Trim().ToLowerInvariant(),
            documents = orderedDocs.Select((doc, index) => new
            {
                rank = index + 1,
                doc.RefType,
                doc.RefId,
                similarity = Math.Round(doc.Similarity, 4),
                updatedAt = doc.UpdatedAt,
                content = TrimContentForPrompt(doc.Content)
            }),
            instructions = new[]
            {
                "Chỉ dùng thông tin trong documents.",
                "Nếu không đủ dữ liệu, trả lời rõ ràng là chưa có thông tin trong hệ thống.",
                "Ưu tiên trả lời tiếng Việt khi language = 'vi'.",
                "Liệt kê mã đơn, ISBN, danh mục khi cần trích dẫn."
            }
        };

        const string systemPrompt = @"Bạn là trợ lý AI nội bộ của hệ thống quản lý nhà sách BookStore.
Nhiệm vụ:
- Chỉ trả lời dựa trên DOCUMENTS được cung cấp bên dưới.
- Nếu câu hỏi ngoài phạm vi dữ liệu trong documents, hãy trả lời rõ ràng: 'Hiện tại tôi chưa có đủ dữ liệu trong hệ thống để trả lời chính xác câu hỏi này.'
- Trình bày câu trả lời ngắn gọn, rõ ràng, ưu tiên tiếng Việt.
- Khi liệt kê, dùng bullet points (-) hoặc đánh số.
- Luôn trích dẫn mã đơn (OrderId), ISBN, CustomerId khi có trong documents.
- Với câu hỏi về số liệu, hãy đưa ra con số cụ thể từ documents.
- Với câu hỏi về sách, ưu tiên thông tin: tiêu đề, ISBN, giá, tồn kho, đánh giá, doanh số.";

        var aiResponseJson = await _geminiClient.CallGeminiAsync(systemPrompt, JsonSerializer.Serialize(payload, CamelCaseSerializerOptions), cancellationToken);
        if (string.IsNullOrWhiteSpace(aiResponseJson))
        {
            return new ApiResponse<AiSearchResponse>
            {
                Success = false,
                Message = "Gemini không trả về kết quả",
                Errors = new List<string> { "Gemini search failed" }
            };
        }

        var answer = ExtractAiSearchAnswer(aiResponseJson, out var metadata);
        var docsForResponse = request.IncludeDebugDocuments ? orderedDocs : new List<AiSearchDocumentDto>();

        return new ApiResponse<AiSearchResponse>
        {
            Success = true,
            Message = "OK",
            Data = new AiSearchResponse
            {
                Answer = answer,
                Documents = docsForResponse,
                Metadata = metadata
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

        var targetRefTypes = new HashSet<string>(seeds.Select(s => s.RefType), StringComparer.OrdinalIgnoreCase);

        if (request.TruncateBeforeInsert)
        {
            var toDelete = _db.AiDocuments.Where(doc => targetRefTypes.Contains(doc.RefType));
            _db.AiDocuments.RemoveRange(toDelete);
            await _db.SaveChangesAsync(cancellationToken);
        }

        Dictionary<(string RefType, string RefId), AiDocument> existingDocs = new();
        if (!request.TruncateBeforeInsert)
        {
            var refTypeList = targetRefTypes.ToList();
            var refIds = seeds.Select(s => s.RefId).Distinct().ToList();
            existingDocs = await _db.AiDocuments
                .Where(doc => refTypeList.Contains(doc.RefType) && refIds.Contains(doc.RefId))
                .ToDictionaryAsync(doc => (doc.RefType, doc.RefId), cancellationToken);
        }

        var indexed = 0;
        var skipped = 0;
        foreach (var seed in seeds)
        {
            var embedding = await _geminiClient.GetEmbeddingAsync(seed.Content, cancellationToken);
            if (embedding.Length == 0)
            {
                skipped++;
                _logger.LogWarning("Skipped indexing {RefType}:{RefId} due to empty embedding", seed.RefType, seed.RefId);
                continue;
            }

            var embeddingJson = JsonSerializer.Serialize(embedding);
            if (!request.TruncateBeforeInsert &&
                existingDocs.TryGetValue((seed.RefType, seed.RefId), out var existing))
            {
                existing.Content = seed.Content;
                existing.EmbeddingJson = embeddingJson;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                await _db.AiDocuments.AddAsync(new AiDocument
                {
                    RefType = seed.RefType,
                    RefId = seed.RefId,
                    Content = seed.Content,
                    EmbeddingJson = embeddingJson,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }

            indexed++;
            if (indexed % 20 == 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Indexed {Indexed} documents, skipped {Skipped}", indexed, skipped);
            }

            // Delay to avoid rate limiting - delay after each request
            await Task.Delay(200, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ApiResponse<AiSearchReindexResponse>
        {
            Success = true,
            Message = "Reindex thành công",
            Data = new AiSearchReindexResponse
            {
                IndexedDocuments = indexed,
                IndexedAt = DateTime.UtcNow,
                RefTypes = seeds.Select(s => s.RefType)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            }
        };
    }

    private async Task<List<AiDocumentSeed>> BuildAiDocumentSeedsAsync(
        AiSearchReindexRequest request,
        IReadOnlyList<string> refTypes,
        CancellationToken cancellationToken)
    {
        var seeds = new List<AiDocumentSeed>();
        var historyDays = Math.Clamp(request.HistoryDays, 30, 365);
        var historySince = DateTime.UtcNow.AddDays(-historyDays);
        var maxBooks = Math.Clamp(request.MaxBooks, 50, 2000);
        var maxOrders = Math.Clamp(request.MaxOrders, 50, 2000);
        var maxCustomers = Math.Clamp(request.MaxCustomers, 50, 2000);

        List<DeliveredOrderLineSnapshot>? deliveredLines = null;
        async Task<List<DeliveredOrderLineSnapshot>> EnsureDeliveredLinesAsync()
        {
            if (deliveredLines != null)
            {
                return deliveredLines;
            }

            deliveredLines = await _db.OrderLines
                .AsNoTracking()
                .Where(ol => ol.Order.PlacedAt >= historySince && ol.Order.Status == OrderStatus.Delivered)
                .Select(ol => new DeliveredOrderLineSnapshot(
                    ol.Isbn,
                    ol.Book != null ? ol.Book.Title : null,
                    ol.Book != null && ol.Book.Category != null ? ol.Book.Category.Name : null,
                    ol.Qty,
                    ol.UnitPrice,
                    ol.Order.PlacedAt))
                .ToListAsync(cancellationToken);

            return deliveredLines;
        }

        List<OrderSummaryRow>? orderSummaries = null;
        async Task<List<OrderSummaryRow>> EnsureOrderSummariesAsync()
        {
            if (orderSummaries != null)
            {
                return orderSummaries;
            }

            orderSummaries = await _db.Orders
                .AsNoTracking()
                .Where(o => o.PlacedAt >= historySince)
                .Select(o => new OrderSummaryRow(
                    o.OrderId,
                    o.CustomerId,
                    o.PlacedAt,
                    o.Status,
                    o.OrderLines.Sum(ol => (int?)ol.Qty) ?? 0,
                    o.OrderLines.Sum(ol => (decimal?)(ol.Qty * ol.UnitPrice)) ?? 0m))
                .ToListAsync(cancellationToken);

            return orderSummaries;
        }

        if (refTypes.Contains("book"))
        {
            var delivered = await EnsureDeliveredLinesAsync();
            var salesLookup = delivered
                .GroupBy(line => line.Isbn)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Quantity = g.Sum(x => x.Quantity),
                        Revenue = g.Sum(x => x.Quantity * x.UnitPrice),
                        LastSoldAt = g.Max(x => x.PlacedAt),
                        Title = g.OrderByDescending(x => x.PlacedAt).FirstOrDefault()?.BookTitle
                    });

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

                if (ratingLookup.TryGetValue(book.Isbn, out var rating))
                {
                    builder.AppendLine($"Đánh giá trung bình: {Math.Round(rating.Item2, 2):0.##}/5 ({rating.Item1} lượt)");
                }

                if (salesLookup.TryGetValue(book.Isbn, out var sale))
                {
                    builder.AppendLine($"Doanh số {historyDays} ngày: {sale.Quantity} bản, doanh thu {FormatCurrency(sale.Revenue)} VNĐ");
                    builder.AppendLine($"Lần bán gần nhất: {sale.LastSoldAt:dd/MM/yyyy HH:mm}");
                }
                else
                {
                    builder.AppendLine($"Trong {historyDays} ngày chưa ghi nhận đơn giao.");
                }

                seeds.Add(new AiDocumentSeed("book", book.Isbn, builder.ToString()));
            }
        }

        if (refTypes.Contains("order"))
        {
            var orders = await _db.Orders
                .AsNoTracking()
                .Where(o => o.PlacedAt >= historySince)
                .Include(o => o.Customer)
                .Include(o => o.OrderLines)
                    .ThenInclude(ol => ol.Book)
                .OrderByDescending(o => o.PlacedAt)
                .Take(maxOrders)
                .ToListAsync(cancellationToken);

            foreach (var order in orders)
            {
                var builder = new StringBuilder();
                var totalAmount = order.OrderLines.Sum(line => line.Qty * line.UnitPrice);
                var totalItems = order.OrderLines.Sum(line => line.Qty);
                var customerName = order.Customer?.FullName ?? $"{order.ReceiverName}".Trim();

                builder.AppendLine("Loại dữ liệu: ORDER");
                builder.AppendLine($"OrderId: {order.OrderId}");
                builder.AppendLine($"Khách hàng: {customerName} (ID {order.CustomerId})");
                builder.AppendLine($"Trạng thái: {GetOrderStatusLabel(order.Status)}");
                builder.AppendLine($"Ngày đặt: {order.PlacedAt:dd/MM/yyyy HH:mm}");
                builder.AppendLine($"Người nhận: {order.ReceiverName}");
                builder.AppendLine($"Địa chỉ nhận: {order.ShippingAddress}");
                builder.AppendLine($"SĐT nhận: {order.ReceiverPhone}");
                if (order.DeliveryAt.HasValue)
                {
                    builder.AppendLine($"Thời gian giao: {order.DeliveryAt:dd/MM/yyyy HH:mm}");
                }
                if (!string.IsNullOrWhiteSpace(order.Note))
                {
                    builder.AppendLine($"Ghi chú: {order.Note}");
                }
                builder.AppendLine($"Tổng sản phẩm: {totalItems}");
                builder.AppendLine($"Giá trị đơn: {FormatCurrency(totalAmount)} VNĐ");
                builder.AppendLine("Chi tiết sản phẩm:");
                foreach (var line in order.OrderLines.OrderByDescending(l => l.Qty))
                {
                    var title = line.Book?.Title ?? line.Isbn;
                    builder.AppendLine($"- {title} (ISBN {line.Isbn}): {line.Qty} x {FormatCurrency(line.UnitPrice)} VNĐ");
                }

                seeds.Add(new AiDocumentSeed("order", order.OrderId.ToString(CultureInfo.InvariantCulture), builder.ToString()));
            }
        }

        if (refTypes.Contains("customer"))
        {
            var summaries = await EnsureOrderSummariesAsync();
            var grouped = summaries
                .GroupBy(row => row.CustomerId)
                .Select(g => new CustomerAggregate(
                    g.Key,
                    g.Count(),
                    g.Count(r => r.Status == OrderStatus.Delivered),
                    g.Sum(r => r.Items),
                    g.Sum(r => r.Revenue),
                    g.Max(r => r.PlacedAt)))
                .OrderByDescending(g => g.TotalSpent)
                .Take(maxCustomers)
                .ToList();

            var customerIds = grouped.Select(g => g.CustomerId).ToList();
            var customers = await _db.Customers
                .AsNoTracking()
                .Where(c => customerIds.Contains(c.CustomerId))
                .ToDictionaryAsync(c => c.CustomerId, cancellationToken);

            foreach (var aggregate in grouped)
            {
                if (!customers.TryGetValue(aggregate.CustomerId, out var customer))
                {
                    continue;
                }

                var builder = new StringBuilder();
                builder.AppendLine("Loại dữ liệu: CUSTOMER");
                builder.AppendLine($"CustomerId: {customer.CustomerId}");
                builder.AppendLine($"Họ tên: {customer.FullName}");
                builder.AppendLine($"Email: {customer.Email ?? "Không có"}");
                builder.AppendLine($"SĐT: {customer.Phone ?? "Không có"}");
                if (!string.IsNullOrWhiteSpace(customer.Address))
                {
                    builder.AppendLine($"Địa chỉ: {customer.Address}");
                }
                if (customer.DateOfBirth.HasValue)
                {
                    builder.AppendLine($"Ngày sinh: {customer.DateOfBirth.Value:dd/MM/yyyy}");
                }
                builder.AppendLine($"Tổng đơn trong {historyDays} ngày: {aggregate.TotalOrders}");
                builder.AppendLine($"Đơn đã giao: {aggregate.DeliveredOrders}");
                builder.AppendLine($"Tổng sản phẩm đã mua: {aggregate.TotalItems}");
                builder.AppendLine($"Tổng chi tiêu ước tính: {FormatCurrency(aggregate.TotalSpent)} VNĐ");
                builder.AppendLine($"Đơn gần nhất: {aggregate.LastOrderAt:dd/MM/yyyy HH:mm}");

                seeds.Add(new AiDocumentSeed("customer", customer.CustomerId.ToString(CultureInfo.InvariantCulture), builder.ToString()));
            }
        }

        if (refTypes.Contains("inventory"))
        {
            var books = await _db.Books
                .AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.Status)
                .ToListAsync(cancellationToken);

            if (books.Count > 0)
            {
                var totalStock = books.Sum(b => b.Stock);
                var totalValue = books.Sum(b => b.Stock * b.AveragePrice);
                var lowStock = books
                    .Where(b => b.Stock <= 5)
                    .OrderBy(b => b.Stock)
                    .ThenBy(b => b.Title)
                    .Take(20)
                    .Select(b => $"{b.Title} (ISBN {b.Isbn}) còn {b.Stock}");
                var highStock = books
                    .Where(b => b.Stock >= 100)
                    .OrderByDescending(b => b.Stock)
                    .ThenBy(b => b.Title)
                    .Take(20)
                    .Select(b => $"{b.Title} (ISBN {b.Isbn}) tồn {b.Stock}");
                var categorySummary = books
                    .GroupBy(b => b.Category?.Name ?? "Không rõ")
                    .Select(g => new
                    {
                        Category = g.Key,
                        Stock = g.Sum(b => b.Stock),
                        Value = g.Sum(b => b.Stock * b.AveragePrice)
                    })
                    .OrderByDescending(g => g.Stock)
                    .Take(10)
                    .ToList();

                var builder = new StringBuilder();
                builder.AppendLine("Loại dữ liệu: INVENTORY");
                builder.AppendLine($"Snapshot (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm}");
                builder.AppendLine($"Tổng đầu sách: {books.Count}");
                builder.AppendLine($"Tổng số lượng tồn: {totalStock}");
                builder.AppendLine($"Giá trị tồn kho ước tính: {FormatCurrency(totalValue)} VNĐ");
                builder.AppendLine("Top danh mục theo tồn kho:");
                foreach (var category in categorySummary)
                {
                    builder.AppendLine($"- {category.Category}: {category.Stock} bản (~{FormatCurrency(category.Value)} VNĐ)");
                }

                if (lowStock.Any())
                {
                    builder.AppendLine("Sản phẩm sắp hết hàng (<=5 cuốn):");
                    foreach (var entry in lowStock)
                    {
                        builder.AppendLine($"- {entry}");
                    }
                }

                if (highStock.Any())
                {
                    builder.AppendLine("Sản phẩm tồn kho cao (>=100 cuốn):");
                    foreach (var entry in highStock)
                    {
                        builder.AppendLine($"- {entry}");
                    }
                }

                seeds.Add(new AiDocumentSeed("inventory", "global-inventory", builder.ToString()));
            }
        }

        if (refTypes.Contains("sales_insight"))
        {
            var delivered = await EnsureDeliveredLinesAsync();
            var summaries = await EnsureOrderSummariesAsync();
            var totalRevenue = delivered.Sum(line => line.Quantity * line.UnitPrice);
            var totalQuantity = delivered.Sum(line => line.Quantity);
            var deliveredOrders = summaries.Count(r => r.Status == OrderStatus.Delivered);
            var cancelledOrders = summaries.Count(r => r.Status == OrderStatus.Cancelled);
            var totalOrders = summaries.Count;
            var avgOrderValue = deliveredOrders > 0 ? totalRevenue / deliveredOrders : 0m;

            var topDays = delivered
                .GroupBy(line => line.PlacedAt.Date)
                .Select(g => new
                {
                    Day = g.Key,
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(g => g.Revenue)
                .Take(7)
                .ToList();

            var topCategories = delivered
                .GroupBy(line => line.CategoryName ?? "Không rõ")
                .Select(g => new
                {
                    Category = g.Key,
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice),
                    Quantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(g => g.Revenue)
                .Take(8)
                .ToList();

            var topBooks = delivered
                .GroupBy(line => new { line.Isbn, line.BookTitle })
                .Select(g => new
                {
                    g.Key.Isbn,
                    Title = string.IsNullOrWhiteSpace(g.Key.BookTitle) ? g.Key.Isbn : g.Key.BookTitle,
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice),
                    Quantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(g => g.Revenue)
                .Take(10)
                .ToList();

            var builder = new StringBuilder();
            builder.AppendLine("Loại dữ liệu: SALES_INSIGHT");
            builder.AppendLine($"Khoảng thời gian: {historySince:dd/MM/yyyy} - {DateTime.UtcNow:dd/MM/yyyy}");
            builder.AppendLine($"Tổng đơn tạo: {totalOrders}, đã giao: {deliveredOrders}, huỷ: {cancelledOrders}");
            builder.AppendLine($"Doanh thu (đơn giao): {FormatCurrency(totalRevenue)} VNĐ");
            builder.AppendLine($"Giá trị trung bình mỗi đơn giao: {FormatCurrency(avgOrderValue)} VNĐ");
            builder.AppendLine($"Tổng số lượng sách bán ra: {totalQuantity}");
            builder.AppendLine("Top ngày doanh thu:");
            foreach (var day in topDays)
            {
                builder.AppendLine($"- {day.Day:dd/MM}: {FormatCurrency(day.Revenue)} VNĐ");
            }
            builder.AppendLine("Top danh mục:");
            foreach (var cat in topCategories)
            {
                builder.AppendLine($"- {cat.Category}: {cat.Quantity} bản ({FormatCurrency(cat.Revenue)} VNĐ)");
            }
            builder.AppendLine("Top sách bán chạy:");
            foreach (var book in topBooks)
            {
                builder.AppendLine($"- {book.Title} (ISBN {book.Isbn}): {book.Quantity} bản ({FormatCurrency(book.Revenue)} VNĐ)");
            }

            seeds.Add(new AiDocumentSeed("sales_insight", "sales-current", builder.ToString()));
        }

        if (refTypes.Contains("purchase_order"))
        {
            var purchaseOrders = await _db.PurchaseOrders
                .AsNoTracking()
                .Where(po => po.OrderedAt >= historySince)
                .Include(po => po.Publisher)
                .Include(po => po.CreatedByEmployee)
                .Include(po => po.Status)
                .Include(po => po.PurchaseOrderLines)
                    .ThenInclude(pol => pol.Book)
                .OrderByDescending(po => po.OrderedAt)
                .Take(maxOrders)
                .ToListAsync(cancellationToken);

            foreach (var po in purchaseOrders)
            {
                var builder = new StringBuilder();
                var totalAmount = po.PurchaseOrderLines.Sum(line => line.QtyOrdered * line.UnitPrice);
                var totalQuantity = po.PurchaseOrderLines.Sum(line => line.QtyOrdered);

                builder.AppendLine("Loại dữ liệu: PURCHASE_ORDER");
                builder.AppendLine($"PO ID: {po.PoId}");
                builder.AppendLine($"Nhà xuất bản: {po.Publisher?.Name ?? "Không rõ"} (ID {po.PublisherId})");
                builder.AppendLine($"Người tạo: {po.CreatedByEmployee?.FullName ?? "Không rõ"} (ID {po.CreatedBy})");
                builder.AppendLine($"Ngày đặt: {po.OrderedAt:dd/MM/yyyy HH:mm}");
                builder.AppendLine($"Trạng thái: {po.Status?.StatusName ?? "Không rõ"}");
                if (!string.IsNullOrWhiteSpace(po.Note))
                {
                    builder.AppendLine($"Ghi chú: {po.Note}");
                }
                builder.AppendLine($"Tổng số lượng: {totalQuantity}");
                builder.AppendLine($"Tổng giá trị: {FormatCurrency(totalAmount)} VNĐ");
                builder.AppendLine("Chi tiết sản phẩm:");
                foreach (var line in po.PurchaseOrderLines.OrderByDescending(l => l.QtyOrdered))
                {
                    var title = line.Book?.Title ?? line.Isbn;
                    builder.AppendLine($"- {title} (ISBN {line.Isbn}): {line.QtyOrdered} x {FormatCurrency(line.UnitPrice)} VNĐ");
                }

                seeds.Add(new AiDocumentSeed("purchase_order", po.PoId.ToString(CultureInfo.InvariantCulture), builder.ToString()));
            }
        }

        if (refTypes.Contains("purchase_order_line"))
        {
            var poLines = await _db.PurchaseOrderLines
                .AsNoTracking()
                .Where(pol => pol.PurchaseOrder.OrderedAt >= historySince)
                .Include(pol => pol.PurchaseOrder)
                    .ThenInclude(po => po.Publisher)
                .Include(pol => pol.Book)
                    .ThenInclude(b => b.Category)
                .OrderByDescending(pol => pol.PurchaseOrder.OrderedAt)
                .Take(maxOrders * 10)
                .ToListAsync(cancellationToken);

            foreach (var line in poLines)
            {
                var builder = new StringBuilder();
                var title = line.Book?.Title ?? line.Isbn;

                builder.AppendLine("Loại dữ liệu: PURCHASE_ORDER_LINE");
                builder.AppendLine($"PO Line ID: {line.PoLineId}");
                builder.AppendLine($"PO ID: {line.PoId}");
                builder.AppendLine($"Sách: {title} (ISBN {line.Isbn})");
                builder.AppendLine($"Danh mục: {line.Book?.Category?.Name ?? "Không rõ"}");
                builder.AppendLine($"Số lượng đặt: {line.QtyOrdered}");
                builder.AppendLine($"Đơn giá: {FormatCurrency(line.UnitPrice)} VNĐ");
                builder.AppendLine($"Tổng giá trị: {FormatCurrency(line.LineTotal)} VNĐ");
                builder.AppendLine($"Ngày đặt PO: {line.PurchaseOrder.OrderedAt:dd/MM/yyyy HH:mm}");
                builder.AppendLine($"Nhà xuất bản: {line.PurchaseOrder.Publisher?.Name ?? "Không rõ"}");

                seeds.Add(new AiDocumentSeed("purchase_order_line", line.PoLineId.ToString(CultureInfo.InvariantCulture), builder.ToString()));
            }
        }

        if (refTypes.Contains("goods_receipt"))
        {
            var goodsReceipts = await _db.GoodsReceipts
                .AsNoTracking()
                .Where(gr => gr.ReceivedAt >= historySince)
                .Include(gr => gr.PurchaseOrder)
                    .ThenInclude(po => po.Publisher)
                .Include(gr => gr.PurchaseOrder)
                    .ThenInclude(po => po.PurchaseOrderLines)
                        .ThenInclude(pol => pol.Book)
                .Include(gr => gr.CreatedByEmployee)
                .Include(gr => gr.GoodsReceiptLines)
                .OrderByDescending(gr => gr.ReceivedAt)
                .Take(maxOrders)
                .ToListAsync(cancellationToken);

            foreach (var gr in goodsReceipts)
            {
                var builder = new StringBuilder();
                var totalAmount = gr.GoodsReceiptLines.Sum(line => line.QtyReceived * line.UnitCost);
                var totalQuantity = gr.GoodsReceiptLines.Sum(line => line.QtyReceived);

                builder.AppendLine("Loại dữ liệu: GOODS_RECEIPT");
                builder.AppendLine($"GR ID: {gr.GrId}");
                builder.AppendLine($"PO ID: {gr.PoId}");
                builder.AppendLine($"Nhà xuất bản: {gr.PurchaseOrder?.Publisher?.Name ?? "Không rõ"}");
                builder.AppendLine($"Người nhận: {gr.CreatedByEmployee?.FullName ?? "Không rõ"} (ID {gr.CreatedBy})");
                builder.AppendLine($"Ngày nhận: {gr.ReceivedAt:dd/MM/yyyy HH:mm}");
                if (!string.IsNullOrWhiteSpace(gr.Note))
                {
                    builder.AppendLine($"Ghi chú: {gr.Note}");
                }
                builder.AppendLine($"Tổng số lượng nhận: {totalQuantity}");
                builder.AppendLine($"Tổng giá trị: {FormatCurrency(totalAmount)} VNĐ");
                builder.AppendLine("Chi tiết sản phẩm đặt (từ PO):");
                if (gr.PurchaseOrder?.PurchaseOrderLines != null)
                {
                    foreach (var poLine in gr.PurchaseOrder.PurchaseOrderLines.OrderByDescending(l => l.QtyOrdered))
                    {
                        var title = poLine.Book?.Title ?? poLine.Isbn;
                        builder.AppendLine($"- {title} (ISBN {poLine.Isbn}): Đặt {poLine.QtyOrdered} x {FormatCurrency(poLine.UnitPrice)} VNĐ");
                    }
                }
                builder.AppendLine($"Số dòng nhận: {gr.GoodsReceiptLines.Count}");

                seeds.Add(new AiDocumentSeed("goods_receipt", gr.GrId.ToString(CultureInfo.InvariantCulture), builder.ToString()));
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

        if (refTypes.Contains("order_line"))
        {
            var orderLines = await _db.OrderLines
                .AsNoTracking()
                .Where(ol => ol.Order.PlacedAt >= historySince)
                .Include(ol => ol.Order)
                    .ThenInclude(o => o.Customer)
                .Include(ol => ol.Book)
                    .ThenInclude(b => b.Category)
                .OrderByDescending(ol => ol.Order.PlacedAt)
                .Take(maxOrders * 10)
                .ToListAsync(cancellationToken);

            foreach (var line in orderLines)
            {
                var builder = new StringBuilder();
                var title = line.Book?.Title ?? line.Isbn;

                builder.AppendLine("Loại dữ liệu: ORDER_LINE");
                builder.AppendLine($"Order Line ID: {line.OrderLineId}");
                builder.AppendLine($"Order ID: {line.OrderId}");
                builder.AppendLine($"Sách: {title} (ISBN {line.Isbn})");
                builder.AppendLine($"Danh mục: {line.Book?.Category?.Name ?? "Không rõ"}");
                builder.AppendLine($"Số lượng: {line.Qty}");
                builder.AppendLine($"Đơn giá: {FormatCurrency(line.UnitPrice)} VNĐ");
                builder.AppendLine($"Tổng giá trị: {FormatCurrency(line.Qty * line.UnitPrice)} VNĐ");
                builder.AppendLine($"Ngày đặt: {line.Order.PlacedAt:dd/MM/yyyy HH:mm}");
                builder.AppendLine($"Khách hàng: {line.Order.Customer?.FullName ?? line.Order.ReceiverName} (ID {line.Order.CustomerId})");
                builder.AppendLine($"Trạng thái đơn: {GetOrderStatusLabel(line.Order.Status)}");

                seeds.Add(new AiDocumentSeed("order_line", line.OrderLineId.ToString(CultureInfo.InvariantCulture), builder.ToString()));
            }
        }

        return seeds;
    }

    private bool TryParseEmbeddingVector(string embeddingJson, out float[] vector)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<List<float>>(embeddingJson);
            if (parsed == null || parsed.Count == 0)
            {
                vector = Array.Empty<float>();
                return false;
            }

            vector = parsed.ToArray();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể parse embedding_json");
            vector = Array.Empty<float>();
            return false;
        }
    }

    private static double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count == 0 || b.Count == 0 || a.Count != b.Count)
        {
            return 0;
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
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

    private static string FormatCurrency(decimal value)
        => string.Format(VietnameseCulture, "{0:0,0}", value);

    private sealed record AiDocumentSeed(string RefType, string RefId, string Content);
    private sealed record DeliveredOrderLineSnapshot(string Isbn, string? BookTitle, string? CategoryName, int Quantity, decimal UnitPrice, DateTime PlacedAt);
    private sealed record OrderSummaryRow(long OrderId, long CustomerId, DateTime PlacedAt, OrderStatus Status, int Items, decimal Revenue);
    private sealed record CustomerAggregate(long CustomerId, int TotalOrders, int DeliveredOrders, int TotalItems, decimal TotalSpent, DateTime LastOrderAt);
}
