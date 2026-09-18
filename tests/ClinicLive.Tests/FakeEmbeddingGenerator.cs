using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;

namespace ClinicLive.Tests;

/// <summary>
/// A stand-in for nomic-embed-text. Deterministic: the same text always yields the same
/// 768 floats, derived from its SHA-256. Tests must never call Ollama — CI has no model,
/// and a test that depends on a running GPU is a test that fails for the wrong reason.
/// </summary>
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int Dimensions = 768;

    /// <summary>Every text this fake was asked to embed, in order — the "did it call the model?" witness.</summary>
    public List<string> Requested { get; } = [];

    /// <summary>How many batches arrived. The ingester promises batches of 16.</summary>
    public int BatchCount { get; private set; }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var batch = values.ToList();
        BatchCount++;
        Requested.AddRange(batch);

        var result = new GeneratedEmbeddings<Embedding<float>>(
            batch.Select(text => new Embedding<float>(Deterministic(text))));

        return Task.FromResult(result);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }

    /// <summary>SHA-256 as the seed of a tiny LCG — stable across runs and machines.</summary>
    public static float[] Deterministic(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var state = BitConverter.ToUInt64(hash, 0) | 1UL;

        var vector = new float[Dimensions];
        for (var i = 0; i < Dimensions; i++)
        {
            state = unchecked(state * 6364136223846793005UL + 1442695040888963407UL);
            vector[i] = (float)((state >> 11) / (double)(1UL << 53) * 2.0 - 1.0);
        }

        return vector;
    }
}
