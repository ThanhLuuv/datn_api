using System.Text.Json;

namespace BookStore.Api.Services;

public interface IGeminiClient
{
    Task<string?> CallGeminiAsync(string systemPrompt, string userPayload, CancellationToken cancellationToken);
    Task<JsonDocument?> CallGeminiCustomAsync(
        object payload,
        string? modelOverride,
        string? baseUrlOverride,
        string? apiKeyOverride,
        CancellationToken cancellationToken,
        string? urlOverride = null);
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken);
    string? ExtractFirstTextFromResponse(JsonDocument doc);

    Task<string?> UploadFileAsync(Stream fileStream, string displayName, string mimeType, CancellationToken cancellationToken);
    Task<string?> CreateFileSearchStoreAsync(string displayName, CancellationToken cancellationToken);
    Task AddFileToStoreAsync(string storeName, string fileUri, CancellationToken cancellationToken);
    Task<string?> GenerateContentWithToolAsync(string systemPrompt, string userPayload, object toolConfig, CancellationToken cancellationToken);

    // Function Calling Methods
    Task<JsonDocument?> CallGeminiWithToolsAsync(string systemPrompt, string userPayload, object toolConfig, CancellationToken cancellationToken);
    Task<string?> SendFunctionResultAsync(string systemPrompt, string userQuery, string functionName, Dictionary<string, object> originalArgs, string functionResult, CancellationToken cancellationToken);
    FunctionCall? TryParseFunctionCall(JsonDocument response);
}
