using ClinicLive.Services;

namespace ClinicLive.Tests;

/// <summary>Records every push the queue tried to send. Nothing leaves the test.</summary>
public sealed class FakePushSender : IPushSender
{
    public List<(IReadOnlyList<string> Tokens, string Title, string Body)> Sent { get; } = [];

    public Task SendAsync(IReadOnlyList<string> tokens, string title, string body, IReadOnlyDictionary<string, string> data)
    {
        Sent.Add((tokens, title, body));
        return Task.CompletedTask;
    }
}
