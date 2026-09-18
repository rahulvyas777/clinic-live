using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ClinicLive.Tests;

/// <summary>
/// A stand-in for qwen2.5 over Ollama. It streams a fixed answer word by word, or — when built
/// with a null answer — throws the moment it is asked, which is how a test proves the model was
/// never called at all.
/// <para>
/// Part 8 adds the other half of a tool-using model: give it <paramref name="callTool"/> and the
/// FIRST request is answered with a function call instead of text, exactly as qwen2.5 answers one.
/// Wrapped in <c>UseFunctionInvocation()</c>, that makes the real loop run — the function is
/// executed against the real service and its JSON comes back as a second request, which this
/// fake answers with <paramref name="answer"/>.
/// </para>
/// </summary>
/// <param name="answer">The text to stream, or null to throw if called at all.</param>
/// <param name="callTool">A function name to request on the first call, or null for plain text.</param>
/// <param name="callArguments">The arguments that call carries.</param>
public sealed class FakeChatClient(
    string? answer,
    string? callTool = null,
    IDictionary<string, object?>? callArguments = null) : IChatClient
{
    private bool _toolRequested;

    /// <summary>How many times the service asked for a completion. The "did it call?" witness.</summary>
    public int Calls { get; private set; }

    /// <summary>The messages of the last call, so a test can read the prompt that was built.</summary>
    public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

    /// <summary>
    /// The options of the last call — where <c>Tools</c> lives. The whole of Part 8's guard is
    /// visible here: on a question that was not about the queue, this carries no tools at all.
    /// </summary>
    public ChatOptions? LastOptions { get; private set; }

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
        LastOptions = options;

        if (callTool is not null && !_toolRequested)
        {
            _toolRequested = true;
            return CallTool(callTool, callArguments, cancellationToken);
        }

        return answer is null
            ? throw new InvalidOperationException("The chat client must not be called for this question.")
            : Stream(answer, cancellationToken);
    }

    /// <summary>
    /// What a tool-using model streams first: a sentence of thinking out loud, then the call
    /// itself. The preamble is here on purpose — qwen2.5 writes one every time, in whichever
    /// language it fancies, and the service has to throw it away.
    /// </summary>
    public const string Preamble = "Let me look at the queue.";

    private static async IAsyncEnumerable<ChatResponseUpdate> CallTool(
        string name,
        IDictionary<string, object?>? arguments,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();

        yield return new ChatResponseUpdate(ChatRole.Assistant, Preamble);
        yield return new ChatResponseUpdate(
            ChatRole.Assistant,
            [new FunctionCallContent("call-1", name, arguments)]);
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
