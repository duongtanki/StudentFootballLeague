using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StudentFootballLeague.BusinessLogic.DTOs
{
    public class MatchScheduleDto : IValidatableObject
    {
        [Required]
        public int TournamentId { get; set; }

        public string? GroupName { get; set; } // 'Group A', 'Group B'

        [Required(ErrorMessage = "Tên vòng đấu là bắt buộc")]
        [StringLength(50)]
        public string RoundName { get; set; } = string.Empty;

        [Required]
        public int HomeTeamId { get; set; }

        [Required]
        public int AwayTeamId { get; set; }

        [Required]
        public DateTime MatchDateTime { get; set; }

        [Required(ErrorMessage = "Tên sân thi đấu là bắt buộc")]
        [StringLength(100)]
        public string PitchName { get; set; } = string.Empty;

        public int? RefereeId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (HomeTeamId == AwayTeamId)
            {
                yield return new ValidationResult("Đội nhà và Đội khách không được trùng nhau.", new[] { nameof(AwayTeamId) });
            }
        }
    }

    public class MatchResultUpdateDto
    {
        [Range(0, 50, ErrorMessage = "Tỷ số từ 0 đến 50")]
        public int HomeScore { get; set; }

        [Range(0, 50, ErrorMessage = "Tỷ số từ 0 đến 50")]
        public int AwayScore { get; set; }

        public int? HomePenalty { get; set; }
        public int? AwayPenalty { get; set; }

        [Required]
        [RegularExpression(@"^(InProgress|Completed|Postponed|Cancelled)$")]
        public string Status { get; set; } = "Completed";

        public List<MatchEventInputDto> Events { get; set; } = new List<MatchEventInputDto>();
    }

    public class MatchEventInputDto
    {
        [Required]
        public int TeamId { get; set; }

        [Required]
        public int PlayerId { get; set; }

        [Required]
        [RegularExpression(@"^(Goal|OwnGoal|PenaltyGoal|YellowCard|RedCard)$")]
        public string EventType { get; set; } = "Goal";

        [Range(1, 120, ErrorMessage = "Phút diễn ra sự kiện từ 1 đến 120")]
        public int Minute { get; set; }

        public string? Description { get; set; }
    }

    public class MatchEventReadDto
    {
        public int EventId { get; set; }
        public int MatchId { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int JerseyNumber { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int Minute { get; set; }
        public string? Description { get; set; }
    }

    public class MatchReadDto
    {
        public int MatchId { get; set; }
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public string? GroupName { get; set; }
        public string RoundName { get; set; } = string.Empty;

        public int HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public string? HomeTeamLogo { get; set; }

        public int AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;
        public string? AwayTeamLogo { get; set; }

        public DateTime MatchDateTime { get; set; }
        public string PitchName { get; set; } = string.Empty;

        public int? RefereeId { get; set; }
        public string? RefereeName { get; set; }

        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public int? HomePenalty { get; set; }
        public int? AwayPenalty { get; set; }
        public string Status { get; set; } = "Scheduled";

        public List<MatchEventReadDto> Events { get; set; } = new List<MatchEventReadDto>();
    }
}
