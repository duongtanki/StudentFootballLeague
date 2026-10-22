using System;

namespace StudentFootballLeague.DataAccess.Entities
{
    public class TeamRegistration
    {
        public int RegistrationId { get; set; }
        public int TournamentId { get; set; }
        public int TeamId { get; set; }
        public string? GroupName { get; set; } // 'Group A', 'Group B'
        public string ApprovalStatus { get; set; } = "Pending"; // Pending, Approved, Rejected
        public string? RejectReason { get; set; }

        // Bảng xếp hạng (Standings)
        public int MatchesPlayed { get; set; } = 0;
        public int Wins { get; set; } = 0;
        public int Draws { get; set; } = 0;
        public int Losses { get; set; } = 0;
        public int GoalsFor { get; set; } = 0;
        public int GoalsAgainst { get; set; } = 0;
        public int Points { get; set; } = 0;
        public int FairPlayPoints { get; set; } = 0; // Thẻ vàng: -1, Thẻ đỏ: -3/-4

        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Tournament? Tournament { get; set; }
        public Team? Team { get; set; }
    }
}
