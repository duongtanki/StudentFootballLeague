using System;
using System.Collections.Generic;

namespace StudentFootballLeague.DataAccess.Entities
{
    public class Tournament
    {
        public int TournamentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime RegistrationDeadline { get; set; }
        public int MaxTeams { get; set; } = 8;
        public int GroupCount { get; set; } = 2; // 2, 3, 4, 6, 8
        public int KnockoutTeamCount { get; set; } = 4; // 0, 4, 8, 16
        public bool HasThirdPlaceMatch { get; set; } = true;
        public int? ChampionTeamId { get; set; }
        public int MinPlayersPerTeam { get; set; } = 7;
        public int MaxPlayersPerTeam { get; set; } = 15;
        public string Status { get; set; } = "Registering"; // Registering, Ongoing, Completed, Cancelled
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Team? ChampionTeam { get; set; }
        public ICollection<TeamRegistration> Registrations { get; set; } = new List<TeamRegistration>();
        public ICollection<Match> Matches { get; set; } = new List<Match>();
    }
}
