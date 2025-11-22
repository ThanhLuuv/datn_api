using System.Text.Json;

namespace BookStore.Api.Services;

public interface IGeminiClient
{
    Task<string?> CallGeminiAsync(string systemPrompt, string userPayload, CancellationToken cancellationToken);
    Task<JsonDocument?> CallGeminiCustomAsync(object payload, string? modelOverride, string? baseUrlOverride, string? apiKeyOverride, CancellationToken cancellationToken);
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken);
    string? ExtractFirstTextFromResponse(JsonDocument doc);
}
