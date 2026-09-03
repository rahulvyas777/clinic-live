namespace ClinicLive.Domain;

/// <summary>
/// A phone that wants push notifications about one appointment. The token is
/// Firebase's address for that app install; it changes over time and one patient
/// may register several phones. Deleted with the appointment.
/// </summary>
public class DeviceRegistration
{
    public long Id { get; set; }
    public long AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    /// <summary>"android" today; "ios" the day someone builds it on a Mac.</summary>
    public required string Platform { get; set; }

    public required string Token { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
