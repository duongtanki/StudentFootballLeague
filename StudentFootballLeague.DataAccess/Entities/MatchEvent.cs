using System;

namespace StudentFootballLeague.DataAccess.Entities
{
    public class MatchEvent
    {
        public int EventId { get; set; }
        public int MatchId { get; set; }
        public int TeamId { get; set; }
        public int PlayerId { get; set; }
        public string EventType { get; set; } = "Goal"; // Goal, OwnGoal, PenaltyGoal, YellowCard, RedCard
        public int Minute { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Match? Match { get; set; }
        public Team? Team { get; set; }
        public Player? Player { get; set; }
    }
}
