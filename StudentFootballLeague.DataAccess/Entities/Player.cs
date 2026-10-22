using System;
using System.Collections.Generic;

namespace StudentFootballLeague.DataAccess.Entities
{
    public class Player
    {
        public int PlayerId { get; set; }
        public int TeamId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public int JerseyNumber { get; set; }
        public string Position { get; set; } = "MF"; // GK, DF, MF, FW
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Team? Team { get; set; }
        public ICollection<MatchEvent> MatchEvents { get; set; } = new List<MatchEvent>();
    }
}
