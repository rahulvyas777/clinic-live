namespace ClinicLive.Domain;

public class ChatMessage
{
    public long Id { get; set; }

    /// <summary>AspNetUsers.Id of the sender.</summary>
    public required string SenderId { get; set; }

    /// <summary>Denormalized display name so the chat never joins AspNetUsers.</summary>
    public required string SenderName { get; set; }

    public required string Body { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
