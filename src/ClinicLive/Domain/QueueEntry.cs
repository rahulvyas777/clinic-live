namespace ClinicLive.Domain;

/// <summary>Created at kiosk check-in. The waiting-room board renders these.</summary>
public class QueueEntry
{
    public long Id { get; set; }
    public long AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;
    public DateTime? CalledAt { get; set; }
}
