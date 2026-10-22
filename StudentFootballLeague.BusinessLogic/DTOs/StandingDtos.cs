using System;

namespace StudentFootballLeague.BusinessLogic.DTOs
{
    public class StandingReadDto
    {
        public int Rank { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? GroupName { get; set; }
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points { get; set; }
        public int FairPlayPoints { get; set; }
    }

    public class TopScorerDto
    {
        public int Rank { get; set; }
        public int PlayerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public int JerseyNumber { get; set; }
        public string Position { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public int GoalsScored { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
    }
}
