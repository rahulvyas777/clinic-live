using Microsoft.AspNetCore.SignalR;

namespace ClinicLive.Hubs;

/// <summary>
/// The queue hub is deliberately "thin": it carries a single signal — QueueChanged —
/// and clients re-query the database for fresh state. Notify, don't ship state:
/// no stale payloads, no ordering races, one source of truth.
/// </summary>
public class QueueHub : Hub
{
    public const string BoardGroup = "board";

    public Task JoinBoard() => Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup);
}
