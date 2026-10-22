using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace StudentFootballLeague.BusinessLogic.DTOs
{
    public class TournamentCreateDto : IValidatableObject
    {
        [Required(ErrorMessage = "Tên giải đấu không được để trống")]
        [StringLength(150, ErrorMessage = "Tên giải đấu không vượt quá 150 ký tự")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kỳ/Năm học không được để trống")]
        [StringLength(20)]
        public string Season { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ngày bắt đầu là bắt buộc")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Ngày kết thúc là bắt buộc")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Hạn chót đăng ký là bắt buộc")]
        public DateTime RegistrationDeadline { get; set; }

        [Range(8, 32, ErrorMessage = "Số đội tối đa chỉ trong khoảng 8, 12, 16, 24, 32")]
        public int MaxTeams { get; set; } = 8;

        [Range(7, 25, ErrorMessage = "Số cầu thủ tối thiểu từ 7 trở lên")]
        public int MinPlayersPerTeam { get; set; } = 7;

        [Range(7, 30, ErrorMessage = "Số cầu thủ tối đa từ 7 đến 30")]
        public int MaxPlayersPerTeam { get; set; } = 15;

        public string Status { get; set; } = "Registering";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate <= StartDate)
            {
                yield return new ValidationResult("Ngày kết thúc (EndDate) phải sau Ngày bắt đầu (StartDate).", new[] { nameof(EndDate) });
            }

            if (RegistrationDeadline > StartDate)
            {
                yield return new ValidationResult("Hạn chót đăng ký (RegistrationDeadline) phải trước hoặc trùng ngày bắt đầu giải đấu.", new[] { nameof(RegistrationDeadline) });
            }

            if (MinPlayersPerTeam > MaxPlayersPerTeam)
            {
                yield return new ValidationResult("Số cầu thủ tối thiểu không được lớn hơn số cầu thủ tối đa.", new[] { nameof(MinPlayersPerTeam) });
            }
        }
    }

    public class TournamentUpdateDto : TournamentCreateDto
    {
    }

    public class TournamentReadDto
    {
        public int TournamentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime RegistrationDeadline { get; set; }
        public int MaxTeams { get; set; }
        public int MinPlayersPerTeam { get; set; }
        public int MaxPlayersPerTeam { get; set; }
        public string Status { get; set; } = string.Empty;
        public int RegisteredTeamsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
