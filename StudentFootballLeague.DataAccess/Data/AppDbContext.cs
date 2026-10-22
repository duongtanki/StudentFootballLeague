using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.DataAccess.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Tournament> Tournaments => Set<Tournament>();
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<Player> Players => Set<Player>();
        public DbSet<TeamRegistration> TeamRegistrations => Set<TeamRegistration>();
        public DbSet<Match> Matches => Set<Match>();
        public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
        public DbSet<PlayerStat> PlayerStats => Set<PlayerStat>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================================================
            // 1. User
            // =========================================================
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.FullName).HasMaxLength(100).IsRequired();
                entity.Property(u => u.Email).HasMaxLength(150).IsRequired();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
                entity.Property(u => u.PhoneNumber).HasMaxLength(15).IsRequired();
                entity.Property(u => u.Role).HasMaxLength(30).HasDefaultValue("TeamManager").IsRequired();

                // Filtered Unique Index cho StudentCode (Cho phép nhiều bản ghi NULL cho Admin/Referee)
                entity.HasIndex(u => u.StudentCode)
                    .IsUnique()
                    .HasFilter("[StudentCode] IS NOT NULL");
            });

            // =========================================================
            // 2. Tournament
            // =========================================================
            modelBuilder.Entity<Tournament>(entity =>
            {
                entity.ToTable("Tournaments");
                entity.HasKey(t => t.TournamentId);
                entity.Property(t => t.Name).HasMaxLength(150).IsRequired();
                entity.HasIndex(t => t.Name).IsUnique();
                entity.Property(t => t.Season).HasMaxLength(20).IsRequired();
                entity.Property(t => t.Status).HasMaxLength(30).HasDefaultValue("Registering").IsRequired();
            });

            // =========================================================
            // 3. Team
            // =========================================================
            modelBuilder.Entity<Team>(entity =>
            {
                entity.ToTable("Teams");
                entity.HasKey(t => t.TeamId);
                entity.Property(t => t.TeamName).HasMaxLength(100).IsRequired();
                entity.HasIndex(t => t.TeamName).IsUnique();
                entity.Property(t => t.FacultyOrClub).HasMaxLength(100).IsRequired();
                entity.Property(t => t.LogoUrl).HasMaxLength(500);
                entity.Property(t => t.HomeKitColor).HasMaxLength(50).IsRequired();

                entity.HasOne(t => t.Manager)
                    .WithMany(u => u.ManagedTeams)
                    .HasForeignKey(t => t.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =========================================================
            // 4. Player
            // =========================================================
            modelBuilder.Entity<Player>(entity =>
            {
                entity.ToTable("Players");
                entity.HasKey(p => p.PlayerId);
                entity.Property(p => p.FullName).HasMaxLength(100).IsRequired();
                entity.Property(p => p.StudentCode).HasMaxLength(20).IsRequired();
                entity.HasIndex(p => p.StudentCode).IsUnique();
                entity.Property(p => p.Position).HasMaxLength(20).IsRequired();
                entity.Property(p => p.AvatarUrl).HasMaxLength(500);

                // Cùng 1 đội bóng không được trùng số áo
                entity.HasIndex(p => new { p.TeamId, p.JerseyNumber }).IsUnique();

                entity.HasOne(p => p.Team)
                    .WithMany(t => t.Players)
                    .HasForeignKey(p => p.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================================================
            // 5. TeamRegistration
            // =========================================================
            modelBuilder.Entity<TeamRegistration>(entity =>
            {
                entity.ToTable("TeamRegistrations");
                entity.HasKey(r => r.RegistrationId);
                entity.Property(r => r.GroupName).HasMaxLength(20);
                entity.Property(r => r.ApprovalStatus).HasMaxLength(20).HasDefaultValue("Pending").IsRequired();
                entity.Property(r => r.RejectReason).HasMaxLength(255);

                // 1 đội chỉ đăng ký 1 lần trong 1 giải đấu
                entity.HasIndex(r => new { r.TournamentId, r.TeamId }).IsUnique();

                entity.HasOne(r => r.Tournament)
                    .WithMany(t => t.Registrations)
                    .HasForeignKey(r => r.TournamentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Team)
                    .WithMany(t => t.Registrations)
                    .HasForeignKey(r => r.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================================================
            // 6. Match
            // =========================================================
            modelBuilder.Entity<Match>(entity =>
            {
                entity.ToTable("Matches");
                entity.HasKey(m => m.MatchId);
                entity.Property(m => m.GroupName).HasMaxLength(20);
                entity.Property(m => m.RoundName).HasMaxLength(50).IsRequired();
                entity.Property(m => m.PitchName).HasMaxLength(100).IsRequired();
                entity.Property(m => m.Status).HasMaxLength(20).HasDefaultValue("Scheduled").IsRequired();

                // Tránh lỗi Multiple Cascade Paths trong SQL Server
                entity.HasOne(m => m.HomeTeam)
                    .WithMany()
                    .HasForeignKey(m => m.HomeTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.AwayTeam)
                    .WithMany()
                    .HasForeignKey(m => m.AwayTeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Tournament)
                    .WithMany(t => t.Matches)
                    .HasForeignKey(m => m.TournamentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Referee)
                    .WithMany(u => u.RefereedMatches)
                    .HasForeignKey(m => m.RefereeId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Check constraint: Đội nhà và khách phải khác nhau
                entity.ToTable(tb => tb.HasCheckConstraint("CK_Matches_DifferentTeams", "[HomeTeamId] <> [AwayTeamId]"));
            });

            // =========================================================
            // 7. MatchEvent
            // =========================================================
            modelBuilder.Entity<MatchEvent>(entity =>
            {
                entity.ToTable("MatchEvents");
                entity.HasKey(e => e.EventId);
                entity.Property(e => e.EventType).HasMaxLength(30).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(255);

                entity.HasOne(e => e.Match)
                    .WithMany(m => m.Events)
                    .HasForeignKey(e => e.MatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Team)
                    .WithMany(t => t.MatchEvents)
                    .HasForeignKey(e => e.TeamId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Player)
                    .WithMany(p => p.MatchEvents)
                    .HasForeignKey(e => e.PlayerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =========================================================
            // 8. PlayerStat (View V_PlayerStats)
            // =========================================================
            modelBuilder.Entity<PlayerStat>(entity =>
            {
                entity.ToView("V_PlayerStats");
                entity.HasKey(ps => new { ps.PlayerId, ps.TournamentId });
            });
        }
    }
}
