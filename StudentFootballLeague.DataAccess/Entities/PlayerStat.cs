using System.ComponentModel.DataAnnotations;

namespace StudentFootballLeague.DataAccess.Entities
{
    public class PlayerStat
    {
        public int PlayerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public int JerseyNumber { get; set; }
        public string Position { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public int TournamentId { get; set; }
        public int GoalsScored { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
    }
}
