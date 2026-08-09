using ClinicLive.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClinicLive.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Patient>(e =>
        {
            e.Property(p => p.FullName).HasMaxLength(200);
            e.Property(p => p.Phone).HasMaxLength(30);
            e.Property(p => p.Email).HasMaxLength(200);
            e.HasIndex(p => p.Phone).IsUnique();
        });

        builder.Entity<Appointment>(e =>
        {
            // Stored as text, not an int — readable in psql, safe to reorder the enum.
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.ConfirmationCode).HasMaxLength(6);
            e.HasIndex(a => a.ConfirmationCode).IsUnique();

            // Two ACTIVE appointments can never share a slot; a cancelled one frees it.
            e.HasIndex(a => a.StartsAt)
                .IsUnique()
                .HasFilter("status NOT IN ('Cancelled', 'NoShow')");
        });

        builder.Entity<QueueEntry>(e =>
        {
            e.HasIndex(q => q.AppointmentId).IsUnique();
        });

        builder.Entity<ChatMessage>(e =>
        {
            e.Property(m => m.SenderName).HasMaxLength(100);
            e.Property(m => m.Body).HasMaxLength(2000);
            e.HasIndex(m => m.SentAt);
        });
    }
}
