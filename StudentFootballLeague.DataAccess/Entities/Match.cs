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
        public string Stage { get; set; } = "Group"; // Group, Knockout
        public int? KnockoutRound { get; set; } // 16 = Vòng 1/8, 8 = Tứ Kết, 4 = Bán Kết, 3 = Tranh Hạng 3, 2 = Chung Kết
        public string? BranchName { get; set; } // Nhánh 1, Nhánh 2, Tranh Hạng 3, Chung Kết
        public int? BracketOrder { get; set; } // Thứ tự cặp đấu trong vòng (1..4)
        public string? HomeSourceLabel { get; set; }
        public string? AwaySourceLabel { get; set; }
        public int? HomeTeamId { get; set; }
        public int? AwayTeamId { get; set; }
        public int? WinnerTeamId { get; set; }
        public int? NextMatchId { get; set; }
        public string? NextMatchSlot { get; set; } // Home, Away
        public int? LoserNextMatchId { get; set; }
        public string? LoserNextMatchSlot { get; set; } // Home, Away
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
        public Team? WinnerTeam { get; set; }
        public Match? NextMatch { get; set; }
        public Match? LoserNextMatch { get; set; }
        public User? Referee { get; set; }
        public ICollection<MatchEvent> Events { get; set; } = new List<MatchEvent>();
    }
}
