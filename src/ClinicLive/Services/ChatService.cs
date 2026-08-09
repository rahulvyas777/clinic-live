using ClinicLive.Data;
using ClinicLive.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClinicLive.Services;

public class ChatService(IDbContextFactory<ApplicationDbContext> dbFactory, ChatRoom room)
{
    public async Task<List<ChatMessageView>> GetRecentAsync(int count = 50)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var recent = await db.ChatMessages
            .OrderByDescending(m => m.SentAt)
            .Take(count)
            .ToListAsync();

        return recent
            .OrderBy(m => m.SentAt)
            .Select(m => new ChatMessageView(m.SenderName, m.Body, m.SentAt))
            .ToList();
    }

    public async Task SendAsync(string senderId, string senderName, string body)
    {
        body = body.Trim();
        if (body.Length is 0 or > 2000)
        {
            return;
        }

        var message = new ChatMessage { SenderId = senderId, SenderName = senderName, Body = body };

        await using var db = await dbFactory.CreateDbContextAsync();
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync();

        room.Broadcast(new ChatMessageView(message.SenderName, message.Body, message.SentAt));
    }
}
