using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StudentFootballLeague.BusinessLogic.DTOs
{
    public class TeamCreateDto
    {
        [Required(ErrorMessage = "Tên đội bóng không được để trống")]
        [StringLength(100, ErrorMessage = "Tên đội không quá 100 ký tự")]
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Khoa/CLB là bắt buộc")]
        [StringLength(100)]
        public string FacultyOrClub { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }

        [Required(ErrorMessage = "Màu áo thi đấu chính là bắt buộc")]
        [StringLength(50)]
        public string HomeKitColor { get; set; } = string.Empty;
    }

    public class TeamUpdateDto : TeamCreateDto
    {
    }

    public class TeamReadDto
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string FacultyOrClub { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public int ManagerId { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public string ManagerEmail { get; set; } = string.Empty;
        public string HomeKitColor { get; set; } = string.Empty;
        public int PlayersCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TeamRegisterTournamentDto
    {
        [Required]
        public int TournamentId { get; set; }
    }

    public class RegistrationApprovalDto
    {
        [Required]
        [RegularExpression(@"^(Approved|Rejected)$", ErrorMessage = "Trạng thái chỉ có thể là Approved hoặc Rejected")]
        public string Status { get; set; } = "Approved";

        public string? GroupName { get; set; } // 'Group A', 'Group B'
        public string? RejectReason { get; set; }
    }

    public class TeamRegistrationReadDto
    {
        public int RegistrationId { get; set; }
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? GroupName { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points { get; set; }
        public int FairPlayPoints { get; set; }
        public DateTime RegisteredAt { get; set; }
    }
}
