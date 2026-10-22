using System;
using System.Collections.Generic;

namespace StudentFootballLeague.DataAccess.Entities
{
    public class Match
    {
        public int MatchId { get; set; }
        public int TournamentId { get; set; }
        public string? GroupName { get; set; } // Phục vụ OData filter theo bảng đấu (ví dụ: 'Group A')
        public string RoundName { get; set; } = string.Empty; // Vòng Bảng - Lượt 1, Tứ Kết, Bán Kết, Chung Kết
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public DateTime MatchDateTime { get; set; }
        public string PitchName { get; set; } = string.Empty;
        public int? RefereeId { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public int? HomePenalty { get; set; }
        public int? AwayPenalty { get; set; }
        public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Postponed, Cancelled
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Tournament? Tournament { get; set; }
        public Team? HomeTeam { get; set; }
        public Team? AwayTeam { get; set; }
        public User? Referee { get; set; }
        public ICollection<MatchEvent> Events { get; set; } = new List<MatchEvent>();
    }
}
