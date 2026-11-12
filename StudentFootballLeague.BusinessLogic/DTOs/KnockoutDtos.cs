using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StudentFootballLeague.BusinessLogic.DTOs
{
    public class GroupStandingsBlockDto
    {
        public string GroupName { get; set; } = string.Empty;
        public List<StandingReadDto> Standings { get; set; } = new List<StandingReadDto>();
    }

    public class QualifiedTeamOptionDto
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? GroupName { get; set; }
        public int RankInGroup { get; set; }
        public int Points { get; set; }
        public int GoalDifference { get; set; }
        public int GoalsFor { get; set; }
        public int FairPlayPoints { get; set; }
        public string DisplayLabel => string.IsNullOrEmpty(GroupName)
            ? TeamName
            : $"{TeamName} ({GroupName} - Hạng {RankInGroup} • {Points}đ)";
    }

    public class KnockoutPairInputDto
    {
        [Range(1, 8)]
        public int BracketOrder { get; set; }

        [StringLength(30)]
        public string BranchName { get; set; } = "Nhánh 1";

        [Required(ErrorMessage = "Vui lòng chọn đội chủ nhà")]
        public int HomeTeamId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn đội khách")]
        public int AwayTeamId { get; set; }

        public string? HomeSourceLabel { get; set; }
        public string? AwaySourceLabel { get; set; }

        [Required(ErrorMessage = "Thời gian thi đấu là bắt buộc")]
        public DateTime MatchDateTime { get; set; }

        [Required(ErrorMessage = "Sân thi đấu là bắt buộc")]
        [StringLength(100)]
        public string PitchName { get; set; } = "Sân cỏ nhân tạo số 1 - Khu Thể chất";

        public int? RefereeId { get; set; }
    }

    public class KnockoutRoundScheduleConfigDto
    {
        public int KnockoutRound { get; set; } // 8 = Tứ Kết, 4 = Bán Kết, 3 = Tranh Hạng 3, 2 = Chung Kết
        public DateTime DefaultDateTime { get; set; }
        public string DefaultPitchName { get; set; } = "Sân Vận Động Trung Tâm";
    }

    public class GenerateKnockoutBracketDto
    {
        public bool HasThirdPlaceMatch { get; set; } = true;

        [Required]
        [MinLength(2, ErrorMessage = "Cần ít nhất 2 cặp đấu (4 đội) để tạo vòng Knockout.")]
        public List<KnockoutPairInputDto> Pairs { get; set; } = new List<KnockoutPairInputDto>();

        public List<KnockoutRoundScheduleConfigDto> SubsequentRounds { get; set; } = new List<KnockoutRoundScheduleConfigDto>();
    }

    public class KnockoutSetupReadDto
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public string TournamentStatus { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int GroupCount { get; set; }
        public int KnockoutTeamCount { get; set; }
        public bool HasThirdPlaceMatch { get; set; }
        public int TotalGroupMatchesCount { get; set; }
        public int CompletedGroupMatchesCount { get; set; }
        public int RemainingGroupMatchesCount { get; set; }
        public bool IsGroupStageCompleted => RemainingGroupMatchesCount == 0 && TotalGroupMatchesCount > 0;
        public bool HasExistingBracket { get; set; }
        public bool CanResetBracket { get; set; }
        public List<GroupStandingsBlockDto> GroupStandings { get; set; } = new List<GroupStandingsBlockDto>();
        public List<QualifiedTeamOptionDto> ApprovedTeams { get; set; } = new List<QualifiedTeamOptionDto>();
        public List<KnockoutPairInputDto> SuggestedPairs { get; set; } = new List<KnockoutPairInputDto>();
    }

    public class KnockoutRoundReadDto
    {
        public int KnockoutRound { get; set; }
        public string RoundName { get; set; } = string.Empty;
        public List<MatchReadDto> Matches { get; set; } = new List<MatchReadDto>();
    }

    public class KnockoutBracketReadDto
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public string TournamentStatus { get; set; } = string.Empty;
        public int KnockoutTeamCount { get; set; }
        public bool HasThirdPlaceMatch { get; set; }
        public int? ChampionTeamId { get; set; }
        public string? ChampionTeamName { get; set; }
        public bool CanResetBracket { get; set; }
        public List<KnockoutRoundReadDto> Rounds { get; set; } = new List<KnockoutRoundReadDto>();
    }
}
