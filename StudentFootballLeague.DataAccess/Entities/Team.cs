using System;
using System.Collections.Generic;

namespace StudentFootballLeague.DataAccess.Entities
{
    public class Team
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string FacultyOrClub { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public int ManagerId { get; set; }
        public string HomeKitColor { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? Manager { get; set; }
        public ICollection<Player> Players { get; set; } = new List<Player>();
        public ICollection<TeamRegistration> Registrations { get; set; } = new List<TeamRegistration>();
        public ICollection<MatchEvent> MatchEvents { get; set; } = new List<MatchEvent>();
    }
}
