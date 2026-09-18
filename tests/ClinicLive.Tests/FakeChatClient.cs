using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ClinicLive.Tests;

/// <summary>
/// A stand-in for qwen2.5 over Ollama. It streams a fixed answer word by word, or — when built
/// with a null answer — throws the moment it is asked, which is how a test proves the model was
/// never called at all.
/// </summary>
public sealed class FakeChatClient(string? answer) : IChatClient
{
    /// <summary>How many times the service asked for a completion. The "did it call?" witness.</summary>
    public int Calls { get; private set; }

    /// <summary>The messages of the last call, so a test can read the prompt that was built.</summary>
    public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The assistant only streams.");

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Counted and checked here, not inside the iterator: an iterator body does not run
        // until something enumerates it, and "never enumerated" must still count as a call.
        Calls++;
        LastMessages = messages.ToList();

        return answer is null
            ? throw new InvalidOperationException("The chat client must not be called for this question.")
            : Stream(answer, cancellationToken);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Stream(
        string text,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var word in text.Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ");
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
