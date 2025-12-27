using System.Globalization;
using System.Text;
using System.Text.Json;
using BookStore.Api.Configuration;
using BookStore.Api.DTOs;
using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace BookStore.Api.Services;

public class TextToSqlService : ITextToSqlService
{
    private readonly TextToSqlOptions _options;
    private readonly string _defaultConnectionString;
    private readonly ILogger<TextToSqlService> _logger;
    private readonly IGeminiClient _geminiClient;

    public TextToSqlService(
        IOptions<TextToSqlOptions> options,
        IConfiguration configuration,
        IGeminiClient geminiClient,
        ILogger<TextToSqlService> logger)
    {
        _options = options.Value;
        _geminiClient = geminiClient;
        _logger = logger;
        _defaultConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    public async Task<ApiResponse<TextToSqlResponse>> AskAsync(TextToSqlRequest request, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return DisabledResponse("Tính năng Text-to-SQL đang được tắt.");
        }

        if (string.IsNullOrWhiteSpace(_options.DbSchema))
        {
            return DisabledResponse("Chưa cấu hình schema cho Text-to-SQL.");
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return new ApiResponse<TextToSqlResponse>
            {
                Success = false,
                Message = "Câu hỏi không được để trống.",
                Errors = new List<string> { "Question is required." }
            };
        }

        try
        {
            var trimmedQuestion = request.Question.Trim();
            var recentMessages = request.RecentMessages ?? new List<TextToSqlChatMessage>();
            var sql = await GenerateSqlFromQuestionAsync(trimmedQuestion, recentMessages, cancellationToken);

            // Trường hợp AI trả về INVALID hoặc không trả về một câu SELECT hợp lệ:
            // -> Hỏi người dùng làm rõ (không lộ chi tiết kỹ thuật) để người dùng cập nhật yêu cầu.
            if (string.Equals(sql, "INVALID", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(sql)
                || !sql.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase))
            {
                var clarification = await GenerateClarificationAsync(trimmedQuestion, recentMessages, cancellationToken);

                return new ApiResponse<TextToSqlResponse>
                {
                    Success = true,
                    Message = "AI cần người dùng làm rõ thêm câu hỏi.",
                    Data = new TextToSqlResponse
                    {
                        Question = trimmedQuestion,
                        SqlQuery = null,
                        Answer = clarification,
                        RowCount = 0,
                        Rows = new List<Dictionary<string, object?>>(),
                        DataPreview = null
                    }
                };
            }

            var rowLimit = Math.Clamp(request.MaxRows ?? _options.MaxRows, 1, 200);

            QueryResult queryResult;
            try
            {
                queryResult = await ExecuteQueryAsync(sql, rowLimit, cancellationToken);
            }
            catch (InvalidOperationException iex)
            {
                // Những lỗi cấu hình (ví dụ: thiếu connection string) là lỗi backend thật sự,
                // trả về lỗi để dev/ops xử lý.
                _logger.LogError(iex, "Text-to-SQL configuration error");
                return new ApiResponse<TextToSqlResponse>
                {
                    Success = false,
                    Message = "Không thể xử lý truy vấn do lỗi cấu hình server.",
                    Errors = new List<string> { iex.Message }
                };
            }
            catch (Exception ex)
            {
                // Nếu thực thi SQL thất bại (ví dụ lỗi cú pháp, runtime), không trả về lỗi kỹ thuật cho người dùng.
                // Thay vào đó, yêu cầu người dùng làm rõ yêu cầu để model tạo lại câu SELECT an toàn.
                _logger.LogWarning(ex, "Text-to-SQL execution failed, asking for clarification");
                var clarification = await GenerateClarificationAsync(trimmedQuestion, recentMessages, cancellationToken);

                return new ApiResponse<TextToSqlResponse>
                {
                    Success = true,
                    Message = "AI cần người dùng làm rõ thêm câu hỏi (lỗi khi thực thi SQL).",
                    Data = new TextToSqlResponse
                    {
                        Question = trimmedQuestion,
                        SqlQuery = null,
                        Answer = clarification,
                        RowCount = 0,
                        Rows = new List<Dictionary<string, object?>>(),
                        DataPreview = null
                    }
                };
            }

            var tablePreview = BuildTablePreview(queryResult.Rows);

            var answer = await GenerateAnswerFromDataAsync(
                trimmedQuestion,
                tablePreview,
                queryResult.Rows.Count == 0,
                recentMessages,
                cancellationToken);

            // Log AI output for debugging
            _logger.LogInformation(
                "\n" +
                "╔════════════════════════════════════════════════════════════════════════════╗\n" +
                "║                      TEXT-TO-SQL AI RESPONSE                               ║\n" +
                "╠════════════════════════════════════════════════════════════════════════════╣\n" +
                "║ Question: {Question}\n" +
                "╠────────────────────────────────────────────────────────────────────────────╣\n" +
                "║ Generated SQL:\n" +
                "║ {SqlQuery}\n" +
                "╠────────────────────────────────────────────────────────────────────────────╣\n" +
                "║ Row Count: {RowCount}\n" +
                "╠────────────────────────────────────────────────────────────────────────────╣\n" +
                "║ AI Answer:\n" +
                "║ {Answer}\n" +
                "╚════════════════════════════════════════════════════════════════════════════╝",
                trimmedQuestion,
                sql,
                queryResult.TotalRows,
                answer);

            return new ApiResponse<TextToSqlResponse>
            {
                Success = true,
                Message = "OK",
                Data = new TextToSqlResponse
                {
                    Question = trimmedQuestion,
                    SqlQuery = sql,
                    Answer = answer,
                    Rows = queryResult.Rows,
                    RowCount = queryResult.TotalRows,
                    DataPreview = tablePreview
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text-to-SQL request failed");
            return new ApiResponse<TextToSqlResponse>
            {
                Success = false,
                Message = "Không thể xử lý yêu cầu Text-to-SQL.",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private ApiResponse<TextToSqlResponse> DisabledResponse(string message)
    {
        return new ApiResponse<TextToSqlResponse>
        {
            Success = false,
            Message = message,
            Errors = new List<string> { message }
        };
    }

    private async Task<string> GenerateSqlFromQuestionAsync(
        string question,
        IReadOnlyList<TextToSqlChatMessage> recentMessages,
        CancellationToken cancellationToken)
    {
        // Cung cấp ngày hiện tại cho model để xử lý các câu hỏi theo thời gian
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        var systemPrompt = $"""
        Bạn là chuyên gia SQL MySQL cho hệ thống quản lý nhà sách.
        Nhiệm vụ của bạn: dựa trên lịch sử hội thoại gần nhất và schema dưới đây để tạo ra đúng MỘT câu lệnh SELECT trả lời cho câu hỏi hiện tại:
        {_options.DbSchema}

        QUAN TRỌNG - Quy tắc nghiệp vụ về trạng thái đơn hàng:
        Bảng `order` có cột `status` (INT) với các giá trị:
        - 0 = PendingConfirmation (Chờ xác nhận) - đơn mới tạo, chưa xử lý
        - 1 = Confirmed (Đã xác nhận) - đang trong quá trình giao hàng
        - 2 = Delivered (Đã giao) - ĐÃ GIAO THÀNH CÔNG, ĐÃ BÁN ĐƯỢC, TÍNH VÀO DOANH THU
        - 3 = Cancelled (Đã hủy) - đơn bị hủy, KHÔNG tính doanh thu

        Khi người dùng hỏi về:
        - "đơn hàng đã bán", "đơn bán được", "đơn thành công", "đơn gần nhất" → WHERE status = 2
        - "doanh thu", "tổng tiền bán được", "thu nhập" → WHERE status = 2
        - "đơn đang giao" → WHERE status = 1
        - "đơn chờ xử lý" → WHERE status = 0
        - "đơn bị hủy" → WHERE status = 3
        Chỉ được phép đọc chứ không được ghi hoặc chỉnh sửa thông tin trong cơ sở dữ liệu.

        Thông tin ngữ cảnh thời gian:
        - Hôm nay (ngày hệ thống, theo UTC) là: {today}.
        - Khi người dùng nói "hôm nay", "hôm qua", "tuần này", "tháng này", hãy hiểu tương đối dựa trên ngày hôm nay và ưu tiên dùng các hàm thời gian của MySQL như CURDATE(), NOW(), WEEK(), MONTH()... thay vì hard-code các ngày cố định trong quá khứ.

        Nguyên tắc:
        - Luôn đọc và hiểu các tin nhắn trước đó để giải nghĩa các đại từ như "người đó", "khách này", "đơn kia"… 
        - Chỉ dùng câu lệnh SELECT (kèm JOIN/WHERE/GROUP BY/ORDER BY/LIMIT nếu cần). Tuyệt đối không dùng các lệnh ghi dữ liệu (UPDATE/DELETE/INSERT/DROP/ALTER...).
        - Chỉ trả về đúng nội dung câu SQL ở dạng text thuần (không markdown, không ```).
        - Nếu câu hỏi nằm ngoài phạm vi dữ liệu của schema (ví dụ hỏi thời tiết, bóng đá, tin tức...), khi đó mới trả về đúng chuỗi: INVALID.
        """;

        var userPayload = JsonSerializer.Serialize(new
        {
            question,
            recentMessages = recentMessages
                .Take(5)
                .Select(m => new { role = m.Role, content = m.Content })
        });

        var content = await _geminiClient.CallGeminiAsync(systemPrompt, userPayload, cancellationToken) ?? string.Empty;

        return content
            .Replace("```sql", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private async Task<QueryResult> ExecuteQueryAsync(string sql, int rowLimit, CancellationToken cancellationToken)
    {
        var connectionString = string.IsNullOrWhiteSpace(_options.ConnectionString)
            ? _defaultConnectionString
            : _options.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Không tìm thấy chuỗi kết nối cho Text-to-SQL.");
        }

        // Log câu SQL để tiện debug và theo dõi
        _logger.LogInformation("TextToSqlService - Executing SQL: {Sql}", sql);

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: 60);
        var rows = await connection.QueryAsync(command);

        var normalizedRows = rows
            .Select(NormalizeRow)
            .ToList();

        return new QueryResult(
            normalizedRows.Take(rowLimit).ToList(),
            normalizedRows.Count);
    }

    private async Task<string> GenerateAnswerFromDataAsync(
        string question,
        string dataPreview,
        bool noData,
        IReadOnlyList<TextToSqlChatMessage> recentMessages,
        CancellationToken cancellationToken)
    {
        var systemPrompt = "Bạn là trợ lý dữ liệu. Hãy dựa vào kết quả SQL để trả lời ngắn gọn, tiếng Việt, dễ hiểu.";

        var userPayload = JsonSerializer.Serialize(new
        {
            question,
            noData,
            data = noData ? "Không tìm thấy bản ghi phù hợp." : dataPreview,
            recentMessages = recentMessages
                .Take(5)
                .Select(m => new { role = m.Role, content = m.Content })
        });

        var content = await _geminiClient.CallGeminiAsync(systemPrompt, userPayload, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Xin lỗi, tôi chưa thể đưa ra câu trả lời.";
        }

        return content.Trim();
    }

    /// <summary>
    /// Khi backend/LLM không sinh được SQL hợp lệ (INVALID), hàm này nhờ Gemini tạo ra
    /// một câu trả lời thân thiện: giải thích là chưa hiểu rõ và hỏi lại đúng trọng tâm.
    /// </summary>
    private async Task<string> GenerateClarificationAsync(
        string question,
        IReadOnlyList<TextToSqlChatMessage> recentMessages,
        CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "Bạn là trợ lý quản lý đơn hàng. Backend thông báo rằng câu hỏi hiện tại chưa đủ rõ để chuyển thành truy vấn SQL an toàn. " +
            "Hãy trả lời NGẮN GỌN như một chatbot bình thường: giải thích là bạn chưa hiểu đủ rõ, và đặt 1–2 câu hỏi làm rõ cụ thể " +
            "(ví dụ: khách nào, mã đơn nào, khoảng thời gian nào...). Không nhắc tới SQL, database, INVALID hay lỗi kỹ thuật.";

        var userPayload = JsonSerializer.Serialize(new
        {
            question,
            recentMessages = recentMessages
                .Take(5)
                .Select(m => new { role = m.Role, content = m.Content })
        });

        var content = await _geminiClient.CallGeminiAsync(systemPrompt, userPayload, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Hiện tại tôi chưa hiểu đủ rõ câu hỏi. Bạn có thể nói cụ thể hơn (ví dụ: khách nào, mã đơn nào, khoảng thời gian nào...) để tôi hỗ trợ chính xác hơn không?";
        }

        return content.Trim();
    }

    private static Dictionary<string, object?> NormalizeRow(object row)
    {
        if (row is IDictionary<string, object> dictionary)
        {
            var dictResult = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in dictionary)
            {
                dictResult[kvp.Key] = kvp.Value;
            }
            return dictResult;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var properties = row.GetType().GetProperties();
        foreach (var property in properties)
        {
            result[property.Name] = property.GetValue(row);
        }
        return result;
    }

    private static string BuildTablePreview(IEnumerable<IDictionary<string, object?>> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return "KẾT QUẢ: Không tìm thấy dữ liệu nào phù hợp.";
        }

        var header = list.First().Keys.ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(" | ", header));
        sb.AppendLine(new string('-', 80));

        foreach (var row in list)
        {
            var values = header
                .Select(col => FormatValue(row.TryGetValue(col, out var value) ? value : null))
                .ToList();
            sb.AppendLine(string.Join(" | ", values));
        }

        return sb.ToString();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "NULL",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            decimal dec => dec.ToString("0.##", CultureInfo.InvariantCulture),
            double dbl => dbl.ToString("0.##", CultureInfo.InvariantCulture),
            float fl => fl.ToString("0.##", CultureInfo.InvariantCulture),
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    private sealed record QueryResult(List<Dictionary<string, object?>> Rows, int TotalRows);
}

