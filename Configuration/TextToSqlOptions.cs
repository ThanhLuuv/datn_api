namespace BookStore.Api.Configuration;

public class TextToSqlOptions
{
    public const string SectionName = "TextToSql";

    /// <summary>
    /// Toggle to quickly disable the feature without removing the endpoint.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional dedicated connection string (read-only user recommended).
    /// Falls back to DefaultConnection when empty.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// OpenAI API key (or Azure OpenAI key).
    /// </summary>
    public string OpenAiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Chat completion model used to translate and synthesize (e.g. gpt-4o-mini).
    /// </summary>
    public string ModelId { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Structured schema prompt that is appended to every translation request.
    /// </summary>
    public string DbSchema { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of rows returned to avoid long prompts.
    /// </summary>
    public int MaxRows { get; set; } = 50;

    /// <summary>
    /// Temperature for both translation and synthesis calls.
    /// </summary>
    public double Temperature { get; set; } = 0.1;
}


