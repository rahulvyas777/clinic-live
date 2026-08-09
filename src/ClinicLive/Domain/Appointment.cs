namespace ClinicLive.Domain;

public enum AppointmentStatus
{
    Booked,
    CheckedIn,
    InProgress,
    Done,
    Cancelled,
    NoShow,
}

public class Appointment
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Slot start, stored as UTC (timestamptz). Slots are 15 minutes.</summary>
    public DateTime StartsAt { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

    /// <summary>The patient's only credential — 6 chars, unambiguous alphabet.</summary>
    public required string ConfirmationCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public QueueEntry? QueueEntry { get; set; }
}
