// public async Task<ApiResponse<AdminAiAssistantResponse>> GetAdminInsightsAsync(
//     AdminAiAssistantRequest request,
//     CancellationToken cancellationToken = default)
// {
//     // 1. Xác định khoảng thời gian phân tích
//     var from = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
//     var to = request.ToDate ?? DateTime.UtcNow;

//     // 2. Lấy báo cáo lợi nhuận (TopSoldItems, TopMarginItems)
//     var profitReport = await _reportService.GetProfitReportAsync(from, to);
//     if (!profitReport.Success || profitReport.Data == null)
//     {
//         return new ApiResponse<AdminAiAssistantResponse>
//         {
//             Success = false,
//             Message = "Không thể lấy dữ liệu báo cáo lợi nhuận để phân tích",
//             Errors = profitReport.Errors
//         };
//     }

//     // 3. Lấy thống kê đánh giá khách hàng cho các sách bán chạy
//     var topIsbns = profitReport.Data.TopSoldItems
//         .Select(x => x.Isbn)
//         .Where(i => !string.IsNullOrWhiteSpace(i))
//         .Distinct()
//         .ToList();

//     var ratingStats = await _db.Ratings
//         .Where(r => topIsbns.Contains(r.Isbn))
//         .GroupBy(r => r.Isbn)
//         .Select(g => new
//         {
//             Isbn = g.Key,
//             AvgStars = g.Average(r => r.Stars),
//             Count = g.Count()
//         })
//         .ToDictionaryAsync(x => x.Isbn, x => new { x.AvgStars, x.Count }, cancellationToken);

//     // 4. Chuẩn bị payload cho AI
//     var payload = new
//     {
//         type = "admin_assistant",
//         language = string.IsNullOrWhiteSpace(request.Language) ? "vi" : request.Language.ToLowerInvariant(),
//         period = new { from = from, to = to },
//         profitSummary = new
//         {
//             profitReport.Data.OrdersCount,
//             profitReport.Data.Revenue,
//             profitReport.Data.CostOfGoods,
//             profitReport.Data.OperatingExpenses,
//             profit = profitReport.Data.Profit
//         },
//         topSoldItems = profitReport.Data.TopSoldItems.Select(i => new
//         {
//             i.Isbn, i.Title, i.QtySold, i.Revenue, i.Cogs, i.Profit,
//             ratings = ratingStats.TryGetValue(i.Isbn, out var rs)
//                 ? new { avgStars = rs.AvgStars, count = rs.Count }
//                 : null
//         }).ToList(),
//         topMarginItems = profitReport.Data.TopMarginItems.Select(i => new
//         {
//             i.Isbn, i.Title, i.QtySold, i.Revenue, i.Cogs, i.Profit, i.MarginPct,
//             ratings = ratingStats.TryGetValue(i.Isbn, out var rs)
//                 ? new { avgStars = rs.AvgStars, count = rs.Count }
//                 : null
//         }).ToList()
//     };

//     // 5. System prompt cho AI
//     var systemPrompt = @"Bạn là trợ lý phân tích dữ liệu bán hàng cho nhà sách.
//     Input: danh sách sách bán chạy, lợi nhuận, và thống kê đánh giá khách hàng.
//     Nhiệm vụ:
//     - Nhận diện các mặt hàng/bộ sách bán chạy.
//     - Gợi ý những danh mục (thể loại) nên ưu tiên nhập thêm.
//     - Gợi ý sách nên:
//       + nhập thêm (nếu đã có và có nguy cơ thiếu hàng),
//       + hoặc nhập mới (nếu thấy thiếu phân khúc, chủ đề).
//     - Tổng hợp các nhận xét nổi bật từ đánh giá (ưu/nhược điểm) và đề xuất cải thiện dịch vụ/chất lượng.

//     TRẢ LỜI DUY NHẤT DƯỚI DẠNG JSON hợp lệ:
//     {
//       ""overview"": ""tóm tắt chung về tình hình bán hàng"",
//       ""recommendedCategories"": [""..."", ""...""],
//       ""bookSuggestions"": [
//         {
//           ""isbn"": ""hoặc rỗng nếu là sách mới"",
//           ""title"": ""tên sách đề xuất"",
//           ""category"": ""thể loại dự kiến"",
//           ""reason"": ""lý do nên nhập / nhập thêm""
//         }
//       ],
//       ""customerFeedbackSummary"": ""tổng hợp các ý chính từ đánh giá và gợi ý cải thiện""
//     }";

//     // 6. Gọi AI
//     var aiResultJson = await _geminiClient.CallGeminiAsync(
//         systemPrompt,
//         JsonSerializer.Serialize(payload),
//         cancellationToken);

//     // 7. Parse kết quả AI
//     var response = new AdminAiAssistantResponse();
//     // ... parse JSON response ...

//     // 8. Enrichment: Làm giàu dữ liệu gợi ý
//     if (response.BookSuggestions.Count > 0)
//     {
//         // Tìm metadata sách (tác giả, NXB, giá...) qua Google Search
//         var enrichTask = EnrichBookSuggestionsAsync(response.BookSuggestions, cancellationToken);
//         // Tìm giá thị trường từ các sàn
//         var priceTask = FetchMarketPriceInsightsAsync(
//             response.BookSuggestions.Select(s => s.Title),
//             cancellationToken);

//         // Chạy song song để tối ưu tốc độ
//         await Task.WhenAll(enrichTask, priceTask);

//         // Cập nhật giá thị trường vào suggestions
//         var priceMap = priceTask.Result;
//         foreach (var suggestion in response.BookSuggestions)
//         {
//             var key = NormalizeTitleKey(suggestion.Title);
//             if (!string.IsNullOrEmpty(key) && priceMap.TryGetValue(key, out var priceInfo))
//             {
//                 suggestion.MarketPrice = priceInfo.MarketPrice;
//                 suggestion.MarketSourceName = priceInfo.SourceName;
//                 suggestion.MarketSourceUrl = priceInfo.SourceUrl;
//             }
//             suggestion.SuggestedPrice ??= TryParseVndPrice(suggestion.MarketPrice);
//             suggestion.SuggestedIsbn ??= await GenerateUniqueIsbnAsync(null, cancellationToken);
//         }
//     }

//     return new ApiResponse<AdminAiAssistantResponse>
//     {
//         Success = true,
//         Message = "Sinh báo cáo gợi ý AI thành công",
//         Data = response
//     };
// }
// public async Task<ApiResponse<AiBookRecommendationResponse>> GetBookRecommendationsAsync(
//     AiBookRecommendationRequest request,
//     CancellationToken cancellationToken = default)
// {
//     // 1. Validation
//     if (string.IsNullOrWhiteSpace(request.Prompt))
//     {
//         return new ApiResponse<AiBookRecommendationResponse>
//         {
//             Success = false,
//             Message = "Prompt is required",
//             Errors = new List<string> { "Prompt không được để trống" }
//         };
//     }

//     var maxResults = Math.Clamp(request.MaxResults, 3, 20);
//     // 2. Lấy danh sách ứng viên từ DB
//     var normalizedSearch = request.Prompt.Trim();
//     var baseBooksQuery = _db.Books
//         .Include(b => b.Category)
//         .Include(b => b.Publisher)
//         .Include(b => b.AuthorBooks)
//             .ThenInclude(ab => ab.Author)
//         .Where(b => b.Status)
//         .AsQueryable();

//     var candidatesQuery = baseBooksQuery;
//     if (!string.IsNullOrWhiteSpace(normalizedSearch))
//     {
//         candidatesQuery = candidatesQuery.Where(b =>
//             EF.Functions.Like(b.Title, $"%{normalizedSearch}%") ||
//             EF.Functions.Like(b.Isbn, $"%{normalizedSearch}%") ||
//             (b.Category != null && EF.Functions.Like(b.Category.Name, $"%{normalizedSearch}%")) ||
//             (b.Publisher != null && EF.Functions.Like(b.Publisher.Name, $"%{normalizedSearch}%")) ||
//             b.AuthorBooks.Any(ab =>
//                 EF.Functions.Like(ab.Author.FirstName + " " + ab.Author.LastName, $"%{normalizedSearch}%")));
//     }

//     // 3. Tính điểm bán chạy 90 ngày
//     var since = DateTime.UtcNow.AddDays(-90);
//     var bestSellerScores = await _db.OrderLines
//         .Include(ol => ol.Order)
//         .Where(ol => ol.Order.Status == OrderStatus.Delivered && ol.Order.PlacedAt >= since)
//         .GroupBy(ol => ol.Isbn)
//         .Select(g => new { Isbn = g.Key, Qty = g.Sum(x => x.Qty) })
//         .ToDictionaryAsync(x => x.Isbn, x => x.Qty, cancellationToken);

//     // 4. Lấy danh sách ứng viên (tối đa 120 cuốn)
//     var candidateBooksRaw = await candidatesQuery
//         .Take(120)
//         .ToListAsync(cancellationToken);

//     // 5. Chuẩn bị payload cho AI
//     var candidatePayload = candidateBooksRaw.Select(b => new
//     {
//         isbn = b.Isbn,
//         title = b.Title,
//         category = b.Category?.Name,
//         publisher = b.Publisher?.Name,
//         publishYear = b.PublishYear,
//         averagePrice = b.AveragePrice,
//         totalSold90d = bestSellerScores.TryGetValue(b.Isbn, out var qty) ? qty : 0,
//         authors = b.AuthorBooks.Select(ab => ab.Author.FirstName + " " + ab.Author.LastName).ToList()
//     }).ToList();

//     // 6. System prompt cho AI
//     var systemPrompt = @"Bạn là trợ lý tư vấn sách cho nhà sách Việt Nam.
//     Nhiệm vụ:
//     - Đọc nhu cầu khách hàng và danh sách sách ứng viên.
//     - Chọn ra tối đa N cuốn phù hợp nhất.
//     - Với mỗi cuốn, viết tóm tắt ngắn (2‑4 câu) và nêu lý do vì sao phù hợp (1‑2 câu).
//     - Ưu tiên sách bán chạy (totalSold90d cao), phù hợp chủ đề, năm xuất bản còn mới, và giá phù hợp.

//     TRẢ LỜI DUY NHẤT DƯỚI DẠNG JSON hợp lệ theo schema:
//     {
//     ""recommendations"": [
//         {
//         ""isbn"": ""..."",
//         ""aiSummary"": ""tóm tắt nội dung & đánh giá"",
//         ""aiReason"": ""lý do cuốn này phù hợp"",
//         ""score"": 0-100
//         }
//     ],
//     ""overallSummary"": ""tóm tắt chung, tối đa 3 câu""
//     }";

//     // 7. Gọi AI
//     var userPrompt = new
//     {
//         type = "book_recommendation",
//         language = "vi",
//         userRequest = request.Prompt,
//         maxResults,
//         candidates = candidatePayload
//     };

//     var aiResultJson = await _geminiClient.CallGeminiAsync(
//         systemPrompt,
//         JsonSerializer.Serialize(userPrompt),
//         cancellationToken);

//     // 8. Parse kết quả và trả về
//     // ... (xử lý JSON response từ AI)
// }
