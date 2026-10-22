using System;
using System.ComponentModel.DataAnnotations;

namespace StudentFootballLeague.BusinessLogic.DTOs
{
    public class PlayerCreateDto
    {
        [Required(ErrorMessage = "MSSV không được để trống")]
        [RegularExpression(@"^[A-Z]{2}\d{6}$", ErrorMessage = "MSSV không đúng định dạng (Ví dụ: HE180101, HA180004)")]
        public string StudentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên cầu thủ không được để trống")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ngày sinh là bắt buộc")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Số áo là bắt buộc")]
        [Range(1, 99, ErrorMessage = "Số áo phải nằm trong khoảng từ 1 đến 99")]
        public int JerseyNumber { get; set; }

        [Required(ErrorMessage = "Vị trí thi đấu là bắt buộc")]
        [RegularExpression(@"^(GK|DF|MF|FW)$", ErrorMessage = "Vị trí chỉ có thể là GK, DF, MF hoặc FW")]
        public string Position { get; set; } = "MF";

        public string? AvatarUrl { get; set; }
    }

    public class PlayerUpdateDto : PlayerCreateDto
    {
        public bool IsActive { get; set; } = true;
    }

    public class PlayerReadDto
    {
        public int PlayerId { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public int JerseyNumber { get; set; }
        public string Position { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
