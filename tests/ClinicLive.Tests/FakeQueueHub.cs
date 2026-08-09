using ClinicLive.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ClinicLive.Tests;

/// <summary>
/// QueueService only calls hub.Clients.Group(...).SendAsync(...) — a no-op stand-in
/// is 30 lines and needs no mocking library. The real-time side is exercised in the
/// browser; these tests care about the database rules.
/// </summary>
public sealed class FakeQueueHub : IHubContext<QueueHub>
{
    public IHubClients Clients { get; } = new NoClients();
    public IGroupManager Groups { get; } = new NoGroups();

    private sealed class NoClients : IHubClients
    {
        public IClientProxy All { get; } = new NoProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new NoProxy();
        public ISingleClientProxy Client(string connectionId) => new NoProxy();
        IClientProxy IHubClients<IClientProxy>.Client(string connectionId) => new NoProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new NoProxy();
        public IClientProxy Group(string groupName) => new NoProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new NoProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new NoProxy();
        public IClientProxy User(string userId) => new NoProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new NoProxy();
    }

    private sealed class NoProxy : ISingleClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default)
            => Task.FromResult(default(T)!);
    }

    private sealed class NoGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
