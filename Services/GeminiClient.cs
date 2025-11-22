using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BookStore.Api.Services;

public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiClient> _logger;

    // Semaphore để giới hạn số request đồng thời đến Gemini API (tránh rate limiting)
    // Static để share giữa các instance nếu service là Transient/Scoped, nhưng tốt nhất là Singleton hoặc Semaphore static
    private static readonly SemaphoreSlim GeminiApiSemaphore = new SemaphoreSlim(3, 3); // Tối đa 3 request đồng thời

    private const string DefaultModel = "gemini-2.5-flash";
    private const string DefaultEmbeddingModel = "text-embedding-004";

    public GeminiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> CallGeminiAsync(string systemPrompt, string userPayload, CancellationToken cancellationToken)
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
                temperature = 0.7
            }
        };

        var doc = await CallGeminiCustomAsync(body, model, baseUrl, apiKey, cancellationToken);
        if (doc == null)
        {
            return null;
        }

        using var docRef = doc;
        return ExtractFirstTextFromResponse(docRef);
    }

    public async Task<JsonDocument?> CallGeminiCustomAsync(object payload, string? modelOverride, string? baseUrlOverride, string? apiKeyOverride, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out var defaultModel, out var defaultBaseUrl, out var defaultApiKey))
        {
            return null;
        }

        var model = string.IsNullOrWhiteSpace(modelOverride) ? defaultModel : modelOverride!;
        var baseUrl = string.IsNullOrWhiteSpace(baseUrlOverride) ? defaultBaseUrl : baseUrlOverride!.TrimEnd('/');
        var apiKey = string.IsNullOrWhiteSpace(apiKeyOverride) ? defaultApiKey : apiKeyOverride!;

        // Log API key đang dùng cho chat
        _logger.LogInformation("Calling Gemini Chat API - Model: {Model}, Key: {ApiKeyPrefix}...{ApiKeySuffix} (Full: {FullKey})", 
            model,
            apiKey?.Length > 10 ? apiKey.Substring(0, 10) : "N/A",
            apiKey?.Length > 10 ? apiKey.Substring(apiKey.Length - 10) : "N/A",
            apiKey ?? "NULL");

        var url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // Sử dụng semaphore để giới hạn số request đồng thời (tránh rate limiting)
        await GeminiApiSemaphore.WaitAsync(cancellationToken);
        try
        {
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Log chi tiết key đang dùng
                var keyPrefix = apiKey?.Length > 20 ? apiKey.Substring(0, 20) : apiKey ?? "NULL";
                var keySuffix = apiKey?.Length > 20 ? apiKey.Substring(apiKey.Length - 10) : "";
                _logger.LogWarning("Gemini API error {StatusCode}: {Content}. Using API key: {ApiKeyPrefix}...{ApiKeySuffix} (Full: {FullKey})", 
                    response.StatusCode, content, keyPrefix, keySuffix, apiKey ?? "NULL");
                
                // Check if it's API key expired error - có thể là rate limiting
                if (content.Contains("API key expired", StringComparison.OrdinalIgnoreCase) || 
                    content.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase))
                {
                    // Có thể là rate limiting, không phải key expired thật sự
                    if (content.Contains("quota", StringComparison.OrdinalIgnoreCase) || 
                        content.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
                        response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Gemini API rate limit/quota exceeded. This may appear as 'API key expired' but the key is valid. Please wait and retry.");
                    }
                    else
                    {
                        _logger.LogError("Gemini API key is expired or invalid. Key: {ApiKeyPrefix}...{ApiKeySuffix}. Please update the API key in appsettings.json or environment variable Gemini__ApiKey", 
                            keyPrefix, keySuffix);
                    }
                }
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
        finally
        {
            GeminiApiSemaphore.Release();
        }
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<float>();
        }

        if (!TryPrepareGeminiRequest(out _, out var baseUrl, out var apiKey))
        {
            return Array.Empty<float>();
        }

        var embeddingModel = Environment.GetEnvironmentVariable("Gemini__EmbeddingModel")
            ?? _configuration["Gemini:EmbeddingModel"]
            ?? DefaultEmbeddingModel;

        // Log API key đang dùng cho embedding
        _logger.LogInformation("Calling Gemini Embedding API - Model: {Model}, Key: {ApiKeyPrefix}...{ApiKeySuffix} (Full: {FullKey})", 
            embeddingModel,
            apiKey?.Length > 10 ? apiKey.Substring(0, 10) : "N/A",
            apiKey?.Length > 10 ? apiKey.Substring(apiKey.Length - 10) : "N/A",
            apiKey ?? "NULL");

        var url = $"{baseUrl}/v1beta/models/{embeddingModel}:embedContent?key={apiKey}";
        var payload = new
        {
            content = new
            {
                parts = new[]
                {
                    new { text = text }
                }
            }
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        // Retry logic với exponential backoff
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            // Sử dụng semaphore để giới hạn số request đồng thời (tránh rate limiting)
            await GeminiApiSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (attempt > 0)
                {
                    var delayMs = (int)(Math.Pow(2, attempt - 1) * 500); // 500ms, 1000ms, 2000ms
                    await Task.Delay(delayMs, cancellationToken);
                    _logger.LogDebug("Retrying Gemini embedding API call, attempt {Attempt}/{MaxRetries}", attempt + 1, maxRetries);
                }

                var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    if (!doc.RootElement.TryGetProperty("embedding", out var embeddingElement) ||
                        !embeddingElement.TryGetProperty("values", out var valuesElement) ||
                        valuesElement.ValueKind != JsonValueKind.Array)
                    {
                        return Array.Empty<float>();
                    }

                    var vector = new List<float>();
                    foreach (var value in valuesElement.EnumerateArray())
                    {
                        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                        {
                            vector.Add((float)number);
                        }
                    }

                    return vector.ToArray();
                }

                // Check if it's API key expired error - don't retry
                if (content.Contains("API key expired", StringComparison.OrdinalIgnoreCase) || 
                    content.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Gemini Embedding API key is expired or invalid. Using API key: {ApiKeyPrefix}... Please update the API key in appsettings.json or environment variable Gemini__ApiKey", 
                        apiKey?.Substring(0, Math.Min(20, apiKey?.Length ?? 0)));
                    return Array.Empty<float>();
                }

                // Check if it's rate limit error - retry
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                    (response.StatusCode == System.Net.HttpStatusCode.BadRequest && content.Contains("quota", StringComparison.OrdinalIgnoreCase)))
                {
                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogWarning("Gemini embedding API rate limited, will retry. Attempt {Attempt}/{MaxRetries}", attempt + 1, maxRetries);
                        continue;
                    }
                }

                // Other errors - log and return empty
                _logger.LogWarning("Gemini embedding API error {StatusCode}: {Content}. Using API key: {ApiKeyPrefix}...", 
                    response.StatusCode, content, apiKey?.Substring(0, Math.Min(20, apiKey?.Length ?? 0)));
                
                if (attempt < maxRetries - 1 && response.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                {
                    // Retry on server errors
                    continue;
                }
                
                return Array.Empty<float>();
            }
            catch (HttpRequestException httpEx) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(httpEx, "HTTP error calling Gemini embedding API, will retry. Attempt {Attempt}/{MaxRetries}", attempt + 1, maxRetries);
                GeminiApiSemaphore.Release();
                continue;
            }
            catch (TaskCanceledException timeoutEx) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(timeoutEx, "Timeout calling Gemini embedding API, will retry. Attempt {Attempt}/{MaxRetries}", attempt + 1, maxRetries);
                GeminiApiSemaphore.Release();
                continue;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Lỗi khi gọi Gemini embedding API");
                GeminiApiSemaphore.Release();
                return Array.Empty<float>();
            }
            finally
            {
                // Release semaphore sau mỗi attempt (nếu chưa release trong catch)
                if (GeminiApiSemaphore.CurrentCount < 3)
                {
                    try { GeminiApiSemaphore.Release(); } catch { }
                }
            }
        }

        return Array.Empty<float>();
    }

    public string? ExtractFirstTextFromResponse(JsonDocument doc)
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

    private bool TryPrepareGeminiRequest(out string model, out string baseUrl, out string apiKey)
    {
        var envKey = Environment.GetEnvironmentVariable("Gemini__ApiKey");
        var configKey = _configuration["Gemini:ApiKey"];
        
        apiKey = envKey ?? configKey ?? string.Empty;
        
        // Log để debug API key đang được dùng
        _logger.LogInformation("Loading Gemini API key - Env var: {HasEnv}, Config: {HasConfig}, Key: {ApiKeyPrefix}...{ApiKeySuffix} (Length: {Length})", 
            !string.IsNullOrEmpty(envKey), 
            !string.IsNullOrEmpty(configKey),
            apiKey?.Length > 10 ? apiKey.Substring(0, 10) : "N/A",
            apiKey?.Length > 10 ? apiKey.Substring(apiKey.Length - 10) : "N/A",
            apiKey?.Length ?? 0);
        
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
}
