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
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Season four, Part 6: the embedding column is vector(768), which only exists
        // once the extension is installed. Declaring it here puts CREATE EXTENSION in
        // the migration, so a fresh database gets it before the table.
        builder.HasPostgresExtension("vector");

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
            // NOTE the explicit name: EF identifies an index by its property list, so
            // two HasIndex(a => a.StartsAt) calls silently MERGE into one — our first
            // hardening attempt renamed this index instead of adding a second one.
            // Distinct names make them distinct indexes.
            e.HasIndex(a => a.StartsAt, "ix_appointments_slot_active_unique")
                .IsUnique()
                .HasFilter("status NOT IN ('Cancelled', 'NoShow')")
                .HasDatabaseName("ix_appointments_slot_active_unique");

            // Part 11 hardening: the partial index above only covers ACTIVE rows, so
            // the all-statuses day-range queries (staff pages) get their own index —
            // EXPLAIN showed a seq scan without it.
            e.HasIndex(a => a.StartsAt, "ix_appointments_starts_at_all")
                .HasDatabaseName("ix_appointments_starts_at_all");

            // Status is stored as text; the database should refuse values the enum
            // doesn't have (a raw UPDATE can bypass every C# check).
            e.ToTable(t => t.HasCheckConstraint("ck_appointments_status",
                "status IN ('Booked', 'CheckedIn', 'InProgress', 'Done', 'Cancelled', 'NoShow')"));
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

        builder.Entity<DeviceRegistration>(e =>
        {
            e.Property(d => d.Platform).HasMaxLength(20);
            e.Property(d => d.Token).HasMaxLength(512);
            // One token belongs to one visit at a time: re-registering the same phone
            // for a new appointment moves it, it doesn't duplicate it.
            e.HasIndex(d => d.Token).IsUnique();
            e.HasIndex(d => d.AppointmentId);
            e.HasOne(d => d.Appointment).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<KnowledgeDocument>(e =>
        {
            e.ToTable("knowledge_document");
            e.Property(d => d.Slug).HasMaxLength(100);
            e.Property(d => d.Title).HasMaxLength(200);
            e.Property(d => d.Audience).HasMaxLength(10);
            e.Property(d => d.SourcePath).HasMaxLength(400);
            e.Property(d => d.ContentHash).HasMaxLength(64);
            e.HasIndex(d => d.Slug).IsUnique().HasDatabaseName("ix_knowledge_document_slug");

            // Visibility is data, not a prompt instruction. A raw INSERT with
            // audience = 'everyone' would quietly leak staff rates to the kiosk.
            e.ToTable(t => t.HasCheckConstraint(
                "ck_knowledge_document_audience", "audience IN ('public', 'staff')"));
        });

        builder.Entity<KnowledgeChunk>(e =>
        {
            e.ToTable("knowledge_chunk");
            e.Property(c => c.Heading).HasMaxLength(200);
            e.Property(c => c.Embedding).HasColumnType("vector(768)");
            e.HasOne(c => c.Document).WithMany(d => d.Chunks).OnDelete(DeleteBehavior.Cascade);

            // Re-ingesting a document deletes its chunks and renumbers from 0; the
            // unique index is what catches a half-finished run leaving a duplicate.
            e.HasIndex(c => new { c.DocumentId, c.Ordinal })
                .IsUnique()
                .HasDatabaseName("ix_knowledge_chunk_document_id_ordinal");

            // Approximate nearest-neighbour search by cosine distance (Part 7 queries it).
            // HNSW builds on an empty table and stays usable while rows are inserted.
            e.HasIndex(c => c.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasDatabaseName("ix_knowledge_chunk_embedding");
        });
    }
}
