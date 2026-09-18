namespace ClinicLive.Services.Ai;

/// <summary>
/// The <c>Ai:</c> section of appsettings, read once at startup the way <see cref="ClinicTime"/>
/// reads <c>Clinic:*</c> — plain IConfiguration, no options pipeline, no validation ceremony.
/// </summary>
/// <param name="Endpoint">Where Ollama listens. 127.0.0.1 only; the app is its only client.</param>
/// <param name="ChatModel">The model that answers (season four, Part 2 chose it).</param>
/// <param name="EmbeddingModel">The model that embeds knowledge chunks (Part 6).</param>
/// <param name="KeepAlive">How long Ollama keeps the model in VRAM between questions.</param>
/// <param name="MaxContextChunks">How many retrieved chunks a grounded answer may use (Part 7).</param>
public record AiOptions(
    string Endpoint,
    string ChatModel,
    string EmbeddingModel,
    string KeepAlive,
    int MaxContextChunks)
{
    /// <summary>Bind from configuration, with the spec's defaults if a key is missing.</summary>
    public static AiOptions FromConfiguration(IConfiguration config) => new(
        config["Ai:Endpoint"] ?? "http://127.0.0.1:11434",
        config["Ai:ChatModel"] ?? "qwen2.5:14b",
        config["Ai:EmbeddingModel"] ?? "nomic-embed-text",
        config["Ai:KeepAlive"] ?? "24h",
        int.TryParse(config["Ai:MaxContextChunks"], out var chunks) ? chunks : 6);
}
