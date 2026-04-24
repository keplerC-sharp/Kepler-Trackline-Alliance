using Kepler_Trackline_Alliance.Models;
using Microsoft.EntityFrameworkCore;

namespace Kepler_Trackline_Alliance.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Operator> Operators { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Participant> Participants { get; set; }
        public DbSet<QueueEntry> QueueEntries { get; set; }
        public DbSet<StintSlot> StintSlots { get; set; }
        public DbSet<SessionLog> SessionLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============================
            // OPERATORS
            // ============================
            modelBuilder.Entity<Operator>(entity =>
            {
                entity.ToTable("operators");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Identifier).HasColumnName("identifier");
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            // ============================
            // SESSIONS
            // ============================
            modelBuilder.Entity<Session>(entity =>
            {
                entity.ToTable("sessions");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.OperatorId).HasColumnName("operator_id");
                entity.Property(e => e.SessionCode).HasColumnName("session_code");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.StartedAt).HasColumnName("started_at");
                entity.Property(e => e.EndedAt).HasColumnName("ended_at");

                entity.HasOne(e => e.Operator)
                      .WithMany()
                      .HasForeignKey(e => e.OperatorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================
            // PARTICIPANTS
            // ============================
            modelBuilder.Entity<Participant>(entity =>
            {
                entity.ToTable("participants");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.GridId).HasColumnName("grid_id");
                entity.Property(e => e.Grade).HasColumnName("grade");
                entity.Property(e => e.SeasonPoints).HasColumnName("season_points");
            });

            // ============================
            // QUEUE_ENTRIES
            // ============================
            modelBuilder.Entity<QueueEntry>(entity =>
            {
                entity.ToTable("queue_entries");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.SessionId).HasColumnName("session_id");
                entity.Property(e => e.ParticipantId).HasColumnName("participant_id");
                entity.Property(e => e.Position).HasColumnName("position");
                entity.Property(e => e.Priority).HasColumnName("priority");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.EstimatedStartS).HasColumnName("estimated_start_s");
                entity.Property(e => e.SessionTimeS).HasColumnName("session_time_s");
                entity.Property(e => e.EnteredAt).HasColumnName("entered_at");

                entity.HasOne(e => e.Session)
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Participant)
                      .WithMany()
                      .HasForeignKey(e => e.ParticipantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.SessionId, e.Status })
                      .HasDatabaseName("idx_queue_session_status");

                entity.HasIndex(e => new { e.SessionId, e.Position })
                      .HasDatabaseName("idx_queue_session_position");

                entity.HasIndex(e => new { e.SessionId, e.ParticipantId })
                      .IsUnique()
                      .HasDatabaseName("uq_session_participant");
            });

            // ============================
            // STINT_SLOTS
            // ============================
            modelBuilder.Entity<StintSlot>(entity =>
            {
                entity.ToTable("stint_slots");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.SessionId).HasColumnName("session_id");
                entity.Property(e => e.QueueEntryId).HasColumnName("queue_entry_id");
                entity.Property(e => e.SlotOrder).HasColumnName("slot_order");
                entity.Property(e => e.SlotStatus).HasColumnName("slot_status");

                entity.HasOne<Session>()
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<QueueEntry>()
                      .WithMany()
                      .HasForeignKey(e => e.QueueEntryId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.SessionId, e.SlotOrder })
                      .HasDatabaseName("idx_stint_session");
            });

            // ============================
            // SESSION_LOG
            // ============================
            modelBuilder.Entity<SessionLog>(entity =>
            {
                entity.ToTable("session_log");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.SessionId).HasColumnName("session_id");
                entity.Property(e => e.OperatorId).HasColumnName("operator_id");
                entity.Property(e => e.ActionType).HasColumnName("action_type");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");

                entity.HasOne<Session>()
                      .WithMany()
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Operator>()
                      .WithMany()
                      .HasForeignKey(e => e.OperatorId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.SessionId, e.CreatedAt })
                      .HasDatabaseName("idx_log_session_time");
            });
        }
        
        
    }
}