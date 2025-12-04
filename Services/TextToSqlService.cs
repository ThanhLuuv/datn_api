using System.Globalization;
using System.Text;
using BookStore.Api.Configuration;
using BookStore.Api.DTOs;
using Dapper;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MySqlConnector;

namespace BookStore.Api.Services;

public class TextToSqlService : ITextToSqlService
{
    private readonly TextToSqlOptions _options;
    private readonly string _defaultConnectionString;
    private readonly ILogger<TextToSqlService> _logger;

    public TextToSqlService(
        IOptions<TextToSqlOptions> options,
        IConfiguration configuration,
        ILogger<TextToSqlService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _defaultConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    public async Task<ApiResponse<TextToSqlResponse>> AskAsync(TextToSqlRequest request, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return DisabledResponse("Tính năng Text-to-SQL đang được tắt.");
        }

        if (string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
        {
            return DisabledResponse("Chưa cấu hình OpenAI API key cho Text-to-SQL.");
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
            var chatService = CreateChatService();
            var trimmedQuestion = request.Question.Trim();
            var sql = await GenerateSqlFromQuestionAsync(chatService, trimmedQuestion, cancellationToken);

            if (string.Equals(sql, "INVALID", StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<TextToSqlResponse>
                {
                    Success = false,
                    Message = "Câu hỏi không thể trả lời bằng dữ liệu hiện có.",
                    Data = new TextToSqlResponse
                    {
                        Question = trimmedQuestion,
                        SqlQuery = sql
                    }
                };
            }

            if (string.IsNullOrWhiteSpace(sql) || !sql.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<TextToSqlResponse>
                {
                    Success = false,
                    Message = "AI không trả về câu lệnh SELECT hợp lệ.",
                    Data = new TextToSqlResponse
                    {
                        Question = trimmedQuestion,
                        SqlQuery = sql
                    },
                    Errors = new List<string> { "Invalid SQL generated." }
                };
            }

            var rowLimit = Math.Clamp(request.MaxRows ?? _options.MaxRows, 1, 200);
            var queryResult = await ExecuteQueryAsync(sql, rowLimit, cancellationToken);
            var tablePreview = BuildTablePreview(queryResult.Rows);

            var answer = await GenerateAnswerFromDataAsync(
                chatService,
                trimmedQuestion,
                tablePreview,
                queryResult.Rows.Count == 0,
                cancellationToken);

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

    private IChatCompletionService CreateChatService()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(_options.ModelId, _options.OpenAiApiKey);
        var kernel = kernelBuilder.Build();
        return kernel.GetRequiredService<IChatCompletionService>();
    }

    private async Task<string> GenerateSqlFromQuestionAsync(
        IChatCompletionService chatService,
        string question,
        CancellationToken cancellationToken)
    {
        var history = new ChatHistory();
        history.AddSystemMessage($"""
Bạn là chuyên gia SQL MySQL.
Chuyển câu hỏi khách hàng thành đúng một câu lệnh SELECT duy nhất dựa trên schema:
{_options.DbSchema}

YÊU CẦU:
1. Chỉ trả về text của câu SQL (không dùng ```).
2. Tuyệt đối cấm UPDATE/DELETE/INSERT/DROP.
3. Nếu không thể trả lời bằng DB này, trả về chữ: INVALID.
4. Với câu hỏi về thời gian, ưu tiên NOW() hoặc CURDATE().
""");
        history.AddUserMessage(question);

        var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        var content = response.Content ?? string.Empty;
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
        IChatCompletionService chatService,
        string question,
        string dataPreview,
        bool noData,
        CancellationToken cancellationToken)
    {
        var history = new ChatHistory();
        history.AddSystemMessage("Bạn là trợ lý dữ liệu. Hãy dựa vào kết quả SQL để trả lời ngắn gọn, tiếng Việt, dễ hiểu.");
        history.AddUserMessage($"""
Câu hỏi: {question}
Dữ liệu:
{(noData ? "Không tìm thấy bản ghi phù hợp." : dataPreview)}
""");

        var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        return response.Content?.Trim() ?? "Xin lỗi, tôi chưa thể đưa ra câu trả lời.";
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

