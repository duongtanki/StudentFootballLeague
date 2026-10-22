using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.DataAccess.Entities;
using StudentFootballLeague.WebClient.Services;

namespace StudentFootballLeague.WebClient.Controllers
{
    [Authorize(Roles = "Admin,Referee")]
    public class AdminController : Controller
    {
        private readonly IApiClientService _apiClient;

        public AdminController(IApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        // =========================================================
        // 1. Phân hệ Quản trị Giải đấu & Duyệt Đội (Admin only)
        // =========================================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Tournaments()
        {
            var tournaments = await _apiClient.GetAsync<List<TournamentReadDto>>("api/tournaments") ?? new List<TournamentReadDto>();
            var registrations = await _apiClient.GetAsync<List<TeamRegistrationReadDto>>("api/registrations") ?? new List<TeamRegistrationReadDto>();

            ViewBag.Tournaments = tournaments;
            ViewBag.Registrations = registrations;

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateTournament(TournamentCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu giải đấu không hợp lệ. Vui lòng kiểm tra lại ngày bắt đầu và kết thúc.";
                return RedirectToAction(nameof(Tournaments));
            }

            var result = await _apiClient.PostAsync<TournamentCreateDto, Tournament>("api/tournaments", dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Không thể tạo giải đấu mới.";
            }
            else
            {
                TempData["Success"] = $"Đã tạo thành công giải đấu '{dto.Name}'!";
            }

            return RedirectToAction(nameof(Tournaments));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveRegistration(int id, string status, string? groupName, string? rejectReason)
        {
            var dto = new RegistrationApprovalDto
            {
                Status = status,
                GroupName = string.IsNullOrEmpty(groupName) ? "Group A" : groupName,
                RejectReason = rejectReason
            };

            var result = await _apiClient.PutAsync($"api/registrations/{id}/approval", dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Thao tác xét duyệt thất bại.";
            }
            else
            {
                TempData["Success"] = status == "Approved" 
                    ? $"Đã phê duyệt đội bóng vào bảng đấu '{dto.GroupName}'!" 
                    : "Đã từ chối hồ sơ đăng ký.";
            }

            return RedirectToAction(nameof(Tournaments));
        }

        // =========================================================
        // 2. Phân hệ Lập lịch & Cập nhật Kết quả (Admin, Referee)
        // =========================================================
        public async Task<IActionResult> Matches()
        {
            var tournaments = await _apiClient.GetAsync<List<TournamentReadDto>>("api/tournaments") ?? new List<TournamentReadDto>();
            var matches = await _apiClient.GetAsync<List<MatchReadDto>>("api/matches") ?? new List<MatchReadDto>();
            var registrations = await _apiClient.GetAsync<List<TeamRegistrationReadDto>>("api/registrations") ?? new List<TeamRegistrationReadDto>();

            ViewBag.Tournaments = tournaments;
            ViewBag.Matches = matches;
            ViewBag.ApprovedTeams = registrations.FindAll(r => r.ApprovalStatus == "Approved");

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ScheduleMatch(MatchScheduleDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu lịch đấu không hợp lệ.";
                return RedirectToAction(nameof(Matches));
            }

            var result = await _apiClient.PostAsync<MatchScheduleDto, Match>("api/matches", dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Không thể lên lịch trận đấu (có thể do trùng sân hoặc trùng đội thi đấu trong 90 phút).";
            }
            else
            {
                TempData["Success"] = "Lên lịch trận đấu thành công!";
            }

            return RedirectToAction(nameof(Matches));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateResult(int matchId, int homeScore, int awayScore, string status)
        {
            var dto = new MatchResultUpdateDto
            {
                HomeScore = homeScore,
                AwayScore = awayScore,
                Status = status,
                Events = new List<MatchEventInputDto>()
            };

            var result = await _apiClient.PutAsync($"api/matches/{matchId}/result", dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Cập nhật tỷ số thất bại.";
            }
            else
            {
                TempData["Success"] = "Cập nhật tỷ số trận đấu thành công! Bảng xếp hạng đã được tự động tính toán lại.";
            }

            return RedirectToAction(nameof(Matches));
        }

        [HttpPost]
        public async Task<IActionResult> AddMatchEvent(int matchId, int teamId, int playerId, string eventType, int minute, string? description)
        {
            var match = await _apiClient.GetAsync<MatchReadDto>($"api/matches/{matchId}");
            if (match == null)
            {
                TempData["Error"] = "Không tìm thấy trận đấu.";
                return RedirectToAction(nameof(Matches));
            }

            var dto = new MatchResultUpdateDto
            {
                HomeScore = match.HomeScore ?? 0,
                AwayScore = match.AwayScore ?? 0,
                Status = match.Status,
                Events = match.Events.ConvertAll(e => new MatchEventInputDto
                {
                    TeamId = e.TeamId,
                    PlayerId = e.PlayerId,
                    EventType = e.EventType,
                    Minute = e.Minute,
                    Description = e.Description
                })
            };

            // Nếu là bàn thắng, tự động tăng tỷ số
            if (eventType == "Goal" || eventType == "PenaltyGoal")
            {
                if (teamId == match.HomeTeamId) dto.HomeScore++;
                else if (teamId == match.AwayTeamId) dto.AwayScore++;
            }

            dto.Events.Add(new MatchEventInputDto
            {
                TeamId = teamId,
                PlayerId = playerId,
                EventType = eventType,
                Minute = minute,
                Description = description
            });

            var result = await _apiClient.PutAsync($"api/matches/{matchId}/result", dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Thêm sự kiện trận đấu thất bại.";
            }
            else
            {
                TempData["Success"] = $"Đã ghi nhận sự kiện ({eventType} ở phút {minute}) và cập nhật Bảng xếp hạng!";
            }

            return RedirectToAction(nameof(Matches));
        }
    }
}
