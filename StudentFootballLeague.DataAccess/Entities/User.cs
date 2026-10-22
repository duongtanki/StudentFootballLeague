using System;
using System.Collections.Generic;

namespace StudentFootballLeague.DataAccess.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? StudentCode { get; set; }
        public string Role { get; set; } = "TeamManager"; // Admin, TeamManager, Referee, Guest
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Team> ManagedTeams { get; set; } = new List<Team>();
        public ICollection<Match> RefereedMatches { get; set; } = new List<Match>();
    }
}
