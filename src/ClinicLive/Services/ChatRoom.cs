using System.Collections.Concurrent;

namespace ClinicLive.Services;

public sealed record ChatMessageView(string SenderName, string Body, DateTime SentAt);

/// <summary>
/// In-process chat state shared by every staff circuit.
///
/// Why no ChatHub? Blazor Server components already ride a SignalR connection —
/// the circuit. A HubConnection opened from a server-side component is a NEW
/// server-to-server connection that does not carry the browser's auth cookie,
/// so an [Authorize] hub answers 401 forever. For same-app, logged-in chat, the
/// circuit + a singleton event bus is the simpler, correct tool; custom hubs
/// (Part 7) are for surfaces that aren't this app's circuits.
/// </summary>
public class ChatRoom
{
    private readonly ConcurrentDictionary<string, int> _online = new();
    private readonly ConcurrentDictionary<string, DateTime> _typing = new();

    public event Action<ChatMessageView>? MessageReceived;
    public event Action? PresenceChanged;
    public event Action? TypingChanged;

    public IReadOnlyList<string> OnlineUsers => _online.Keys.OrderBy(n => n).ToList();

    public IReadOnlyList<string> TypingUsers =>
        _typing.Where(kv => kv.Value > DateTime.UtcNow.AddSeconds(-3))
               .Select(kv => kv.Key)
               .OrderBy(n => n)
               .ToList();

    public void Join(string name)
    {
        _online.AddOrUpdate(name, 1, (_, count) => count + 1);
        PresenceChanged?.Invoke();
    }

    public void Leave(string name)
    {
        if (_online.AddOrUpdate(name, 0, (_, count) => count - 1) <= 0)
        {
            _online.TryRemove(name, out _);
        }
        _typing.TryRemove(name, out _);
        PresenceChanged?.Invoke();
    }

    public void Broadcast(ChatMessageView message)
    {
        _typing.TryRemove(message.SenderName, out _);
        MessageReceived?.Invoke(message);
        TypingChanged?.Invoke();
    }

    public void SetTyping(string name)
    {
        _typing[name] = DateTime.UtcNow;
        TypingChanged?.Invoke();
    }
}
