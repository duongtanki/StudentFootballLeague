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
        public int MinPlayersPerTeam { get; set; } = 7;
        public int MaxPlayersPerTeam { get; set; } = 15;
        public string Status { get; set; } = "Registering"; // Registering, Ongoing, Completed, Cancelled
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<TeamRegistration> Registrations { get; set; } = new List<TeamRegistration>();
        public ICollection<Match> Matches { get; set; } = new List<Match>();
    }
}
