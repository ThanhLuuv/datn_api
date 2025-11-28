using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BookStore.Api.Services;

// ============================================================================
// Function Calling Support Classes
// ============================================================================

public record FunctionCall(string Name, Dictionary<string, object> Args);

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

    public async Task<JsonDocument?> CallGeminiCustomAsync(object payload, string? modelOverride, string? baseUrlOverride, string? apiKeyOverride, CancellationToken cancellationToken, string? urlOverride = null)
    {
        if (!TryPrepareGeminiRequest(out var defaultModel, out var defaultBaseUrl, out var defaultApiKey))
        {
            return null;
        }

        var model = string.IsNullOrWhiteSpace(modelOverride) ? defaultModel : modelOverride!;
        var baseUrl = string.IsNullOrWhiteSpace(baseUrlOverride) ? defaultBaseUrl : baseUrlOverride!.TrimEnd('/');
        var apiKey = string.IsNullOrWhiteSpace(apiKeyOverride) ? defaultApiKey : apiKeyOverride!;

        // Log API key đang dùng cho chat
        _logger.LogInformation("Calling Gemini API - Model: {Model}, Key: {ApiKeyPrefix}...{ApiKeySuffix} (Full: {FullKey})", 
            model,
            apiKey?.Length > 10 ? apiKey.Substring(0, 10) : "N/A",
            apiKey?.Length > 10 ? apiKey.Substring(apiKey.Length - 10) : "N/A",
            apiKey ?? "NULL");

        var url = urlOverride;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";
        }

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
    public async Task<string?> UploadFileAsync(Stream fileStream, string displayName, string mimeType, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out _, out var baseUrl, out var apiKey))
        {
            return null;
        }

        // Upload API uses a different base URL usually, but for Gemini it's often the same or 'https://generativelanguage.googleapis.com'
        // The upload endpoint is /upload/v1beta/files
        var uploadUrl = $"https://generativelanguage.googleapis.com/upload/v1beta/files?key={apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Add("X-Goog-Upload-Protocol", "raw");
        request.Headers.Add("X-Goog-Upload-Header-Content-Length", fileStream.Length.ToString());
        request.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
        
        // Wrap stream in StreamContent
        var content = new StreamContent(fileStream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        request.Content = content;

        // Add display name in metadata if possible, but raw upload doesn't support metadata easily in one go.
        // For simplicity, we just upload. To set display name, we might need multipart.
        // Let's stick to raw for now.

        await GeminiApiSemaphore.WaitAsync(cancellationToken);
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to upload file. Status: {Status}, Body: {Body}", response.StatusCode, responseContent);
                return null;
            }

            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("file", out var fileElement) && 
                fileElement.TryGetProperty("name", out var nameElement)) // 'name' is the URI (files/...)
            {
                return nameElement.GetString();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Gemini");
            return null;
        }
        finally
        {
            GeminiApiSemaphore.Release();
        }
    }

    public async Task<string?> CreateFileSearchStoreAsync(string displayName, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out _, out var baseUrl, out var apiKey))
        {
            return null;
        }

        var url = $"{baseUrl}/v1beta/fileSearchStores?key={apiKey}";
        var body = new { displayName = displayName };

        var doc = await CallGeminiCustomAsync(body, null, baseUrl, apiKey, cancellationToken, url); // Pass url explicitly
        if (doc == null) return null;

        if (doc.RootElement.TryGetProperty("name", out var nameElement))
        {
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
    public async Task<string?> UploadFileAsync(Stream fileStream, string displayName, string mimeType, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out _, out var baseUrl, out var apiKey))
        {
            return null;
        }

        // Upload API uses a different base URL usually, but for Gemini it's often the same or 'https://generativelanguage.googleapis.com'
        // The upload endpoint is /upload/v1beta/files
        var uploadUrl = $"https://generativelanguage.googleapis.com/upload/v1beta/files?key={apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Add("X-Goog-Upload-Protocol", "raw");
        request.Headers.Add("X-Goog-Upload-Header-Content-Length", fileStream.Length.ToString());
        request.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
        
        // Wrap stream in StreamContent
        var content = new StreamContent(fileStream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        request.Content = content;

        // Add display name in metadata if possible, but raw upload doesn't support metadata easily in one go.
        // For simplicity, we just upload. To set display name, we might need multipart.
        // Let's stick to raw for now.

        await GeminiApiSemaphore.WaitAsync(cancellationToken);
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to upload file. Status: {Status}, Body: {Body}", response.StatusCode, responseContent);
                return null;
            }

            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("file", out var fileElement) && 
                fileElement.TryGetProperty("name", out var nameElement)) // 'name' is the URI (files/...)
            {
                return nameElement.GetString();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Gemini");
            return null;
        }
        finally
        {
            GeminiApiSemaphore.Release();
        }
    }

    public async Task<string?> CreateFileSearchStoreAsync(string displayName, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out _, out var baseUrl, out var apiKey))
        {
            return null;
        }

        var url = $"{baseUrl}/v1beta/fileSearchStores?key={apiKey}";
        var body = new { displayName = displayName };

        var doc = await CallGeminiCustomAsync(body, null, baseUrl, apiKey, cancellationToken, url); // Pass url explicitly
        if (doc == null) return null;

        if (doc.RootElement.TryGetProperty("name", out var nameElement))
        {
            return nameElement.GetString();
        }
        return null;
    }

    public async Task AddFileToStoreAsync(string storeName, string fileUri, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out _, out var baseUrl, out var apiKey))
        {
            return;
        }

        // Endpoint: POST /v1beta/{storeName}/files
        var url = $"{baseUrl}/v1beta/{storeName}/files?key={apiKey}";
        var body = new { resourceName = fileUri };

        // We can use CallGeminiCustomAsync but we don't expect a return value really, just success.
        // But CallGeminiCustomAsync expects a response body.
        
        // Let's manually call to handle void return or check errors
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        await GeminiApiSemaphore.WaitAsync(cancellationToken);
        try
        {
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to add file to store. Status: {Status}, Body: {Body}", response.StatusCode, content);
                throw new Exception($"Failed to add file to store: {content}");
            }
        }
        finally
        {
            GeminiApiSemaphore.Release();
        }
    }

    public async Task<string?> UploadFileToStoreAsync(Stream fileStream, string storeName, string mimeType, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out _, out var baseUrl, out var apiKey))
        {
            return null;
        }

        // Endpoint: POST https://generativelanguage.googleapis.com/upload/v1beta/{storeName}:upload?key={apiKey}
        // Note: storeName includes "fileSearchStores/..."
        var uploadUrl = $"https://generativelanguage.googleapis.com/upload/v1beta/{storeName}:upload?key={apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        request.Headers.Add("X-Goog-Upload-Protocol", "raw");
        request.Headers.Add("X-Goog-Upload-Header-Content-Length", fileStream.Length.ToString());
        request.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
        
        var content = new StreamContent(fileStream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        request.Content = content;

        await GeminiApiSemaphore.WaitAsync(cancellationToken);
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to upload file to store. Status: {Status}, Body: {Body}", response.StatusCode, responseContent);
                return null;
            }

            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("file", out var fileElement) && 
                fileElement.TryGetProperty("name", out var nameElement))
            {
                return nameElement.GetString();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to store Gemini");
            return null;
        }
        finally
        {
            GeminiApiSemaphore.Release();
        }
    }

    public async Task<string?> GenerateContentWithToolAsync(string systemPrompt, string userPayload, object toolConfig, CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out var model, out var baseUrl, out var apiKey))
        {
            return null;
        }

        var url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";
        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPayload } }
                }
            },
            tools = new[] { toolConfig },
            generationConfig = new { temperature = 0.5 }
        };

        var doc = await CallGeminiCustomAsync(body, model, baseUrl, apiKey, cancellationToken);
        if (doc == null) return null;

        using var docRef = doc;
        return ExtractFirstTextFromResponse(docRef);
    }

    // ============================================================================
    // Function Calling Methods
    // ============================================================================

    public async Task<JsonDocument?> CallGeminiWithToolsAsync(
        string systemPrompt, 
        string userPayload, 
        object toolConfig, 
        CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out var model, out var baseUrl, out var apiKey))
        {
            return null;
        }

        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPayload } }
                }
            },
            tools = new[] { toolConfig },
            generationConfig = new { temperature = 0.3 }
        };

        return await CallGeminiCustomAsync(body, model, baseUrl, apiKey, cancellationToken);
    }

    public async Task<string?> SendFunctionResultAsync(
        string systemPrompt,
        string userQuery,
        string functionName,
        Dictionary<string, object> originalArgs,
        string functionResult,
        CancellationToken cancellationToken)
    {
        if (!TryPrepareGeminiRequest(out var model, out var baseUrl, out var apiKey))
        {
            return null;
        }

        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userQuery } }
                },
                new
                {
                    role = "model",
                    parts = new[]
                    {
                        new
                        {
                            functionCall = new
                            {
                                name = functionName,
                                args = originalArgs // FIXED: Truyền args gốc thay vì {}
                            }
                        }
                    }
                },
                new
                {
                    role = "function",
                    parts = new[]
                    {
                        new
                        {
                            functionResponse = new
                            {
                                name = functionName,
                                response = new
                                {
                                    content = functionResult
                                }
                            }
                        }
                    }
                }
            },
            generationConfig = new { temperature = 0.3 }
        };

        var doc = await CallGeminiCustomAsync(body, model, baseUrl, apiKey, cancellationToken);
        if (doc == null) return null;

        using var docRef = doc;
        return ExtractFirstTextFromResponse(docRef);
    }

    public FunctionCall? TryParseFunctionCall(JsonDocument response)
    {
        try
        {
            if (!response.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts))
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("functionCall", out var functionCall))
                    {
                        var name = functionCall.TryGetProperty("name", out var nameProp) 
                            ? nameProp.GetString() ?? string.Empty 
                            : string.Empty;

                        var args = new Dictionary<string, object>();
                        if (functionCall.TryGetProperty("args", out var argsProp))
                        {
                            foreach (var arg in argsProp.EnumerateObject())
                            {
                                args[arg.Name] = arg.Value.ValueKind switch
                                {
                                    JsonValueKind.String => arg.Value.GetString() ?? string.Empty,
                                    JsonValueKind.Number => arg.Value.GetDouble(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    _ => arg.Value.GetRawText()
                                };
                            }
                        }

                        return new FunctionCall(name, args);
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing function call from Gemini response");
            return null;
        }
    }
}
