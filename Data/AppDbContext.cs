using Kepler_Trackline_Alliance.Models;
using Microsoft.EntityFrameworkCore;

namespace Kepler_Trackline_Alliance.Data
{
    /// <summary>
    /// Centralized Database Context for the Kepler Trackline Alliance system.
    /// Orchestrates ORM mappings between the domain models and the underlying MySQL schema.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Operator>    Operators    { get; set; }
        public DbSet<Session>     Sessions     { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<QueueEntry>  QueueEntries { get; set; }
        public DbSet<StintSlot>   StintSlots   { get; set; }
        public DbSet<SessionLog>  SessionLogs  { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── OPERATORS ────────────────────────────────────────────────
            // Manages authorized personnel credentials and access roles.
            modelBuilder.Entity<Operator>(e =>
            {
                e.ToTable("operators");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.Identifier).HasColumnName("identifier");
                e.Property(x => x.FullName).HasColumnName("full_name");
                e.Property(x => x.PasswordHash).HasColumnName("password_hash");
                e.Property(x => x.Role).HasColumnName("role");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
            });

            // ── SESSIONS ─────────────────────────────────────────────────
            // Tracks discrete blocks of track activity (e.g., morning sessions, race events).
            modelBuilder.Entity<Session>(e =>
            {
                e.ToTable("sessions");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.OperatorId).HasColumnName("operator_id");
                e.Property(x => x.SessionCode).HasColumnName("session_code");
                e.Property(x => x.Status).HasColumnName("status");
                e.Property(x => x.StartedAt).HasColumnName("started_at");
                e.Property(x => x.EndedAt).HasColumnName("ended_at");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.HasOne(x => x.Operator)
                 .WithMany()
                 .HasForeignKey(x => x.OperatorId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── PARTICIPANTS ─────────────────────────────────────────────
            // Persistent registry of all drivers registered in the alliance system.
            modelBuilder.Entity<Participant>(e =>
            {
                e.ToTable("participants");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.FullName).HasColumnName("full_name");
                e.Property(x => x.GridId).HasColumnName("grid_id");
                e.Property(x => x.Grade).HasColumnName("grade");
                e.Property(x => x.SeasonPoints).HasColumnName("season_points");
                e.Property(x => x.RegisteredAt).HasColumnName("registered_at");
            });

            // ── QUEUE_ENTRIES ─────────────────────────────────────────────
            // Junction table managing the real-time state of the track queue.
            modelBuilder.Entity<QueueEntry>(e =>
            {
                e.ToTable("queue_entries");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.SessionId).HasColumnName("session_id");
                e.Property(x => x.ParticipantId).HasColumnName("participant_id");
                e.Property(x => x.Position).HasColumnName("position");
                e.Property(x => x.Priority).HasColumnName("priority");
                e.Property(x => x.Status).HasColumnName("status");
                e.Property(x => x.EstimatedStartS).HasColumnName("estimated_start_s");
                e.Property(x => x.SessionTimeS).HasColumnName("session_time_s");
                e.Property(x => x.EnteredAt).HasColumnName("entered_at");
                e.Property(x => x.StartedAt).HasColumnName("started_at");
                e.Property(x => x.CompletedAt).HasColumnName("completed_at");

                e.HasOne(x => x.Session)
                 .WithMany()
                 .HasForeignKey(x => x.SessionId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Participant)
                 .WithMany()
                 .HasForeignKey(x => x.ParticipantId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Optimization: Indexes to support high-frequency SignalR broadcasts and polling.
                e.HasIndex(x => new { x.SessionId, x.Status })
                 .HasDatabaseName("idx_queue_session_status");

                e.HasIndex(x => new { x.SessionId, x.Position })
                 .HasDatabaseName("idx_queue_session_position");

                // Note: No unique constraint on SessionId/ParticipantId to allow re-entry
                // after completion or cancellation. Concurrency control is handled in QueueService.
                e.HasIndex(x => new { x.SessionId, x.ParticipantId })
                 .HasDatabaseName("idx_session_participant");
            });

            // ── STINT_SLOTS ───────────────────────────────────────────────
            // Maps specific time/order slots to session queue entries.
            modelBuilder.Entity<StintSlot>(e =>
            {
                e.ToTable("stint_slots");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.SessionId).HasColumnName("session_id");
                e.Property(x => x.QueueEntryId).HasColumnName("queue_entry_id");
                e.Property(x => x.SlotOrder).HasColumnName("slot_order");
                e.Property(x => x.SlotStatus).HasColumnName("slot_status");
                e.Property(x => x.AssignedAt).HasColumnName("assigned_at");

                e.HasOne<Session>()
                 .WithMany()
                 .HasForeignKey(x => x.SessionId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne<QueueEntry>()
                 .WithMany()
                 .HasForeignKey(x => x.QueueEntryId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(x => new { x.SessionId, x.SlotOrder })
                 .HasDatabaseName("idx_stint_session");
            });

            // ── SESSION_LOG ───────────────────────────────────────────────
            // Immutable audit trail for all administrative actions taken during a session.
            modelBuilder.Entity<SessionLog>(e =>
            {
                e.ToTable("session_log");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.SessionId).HasColumnName("session_id");
                e.Property(x => x.OperatorId).HasColumnName("operator_id");
                e.Property(x => x.ActionType).HasColumnName("action_type").HasMaxLength(50);
                e.Property(x => x.Notes).HasColumnName("notes");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");

                e.HasOne<Session>()
                 .WithMany()
                 .HasForeignKey(x => x.SessionId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne<Operator>()
                 .WithMany()
                 .HasForeignKey(x => x.OperatorId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(x => new { x.SessionId, x.CreatedAt })
                 .HasDatabaseName("idx_log_session_time");
            });
        }
    }
}
