using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.DataAccess.Entities;
using StudentFootballLeague.WebClient.Services;

namespace StudentFootballLeague.WebClient.Controllers
{
    [Authorize(Roles = "TeamManager")]
    public class MyTeamController : Controller
    {
        private readonly IApiClientService _apiClient;

        public MyTeamController(IApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Dashboard()
        {
            var team = await _apiClient.GetAsync<Team>("api/teams/my-team");
            var tournaments = await _apiClient.GetAsync<List<TournamentReadDto>>("api/tournaments?status=Registering") ?? new List<TournamentReadDto>();

            ViewBag.AvailableTournaments = tournaments;
            return View(team);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeam(TeamCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin đội bóng.";
                return RedirectToAction(nameof(Dashboard));
            }

            var result = await _apiClient.PostAsync<TeamCreateDto, Team>("api/teams", dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Không thể tạo hồ sơ đội bóng.";
            }
            else
            {
                TempData["Success"] = "Tạo hồ sơ đội bóng thành công! Bạn có thể bắt đầu thêm cầu thủ vào đội.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> AddPlayer(PlayerCreateDto dto)
        {
            var team = await _apiClient.GetAsync<Team>("api/teams/my-team");
            if (team == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin đội bóng.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu cầu thủ không hợp lệ. Vui lòng kiểm tra lại MSSV, số áo và vị trí.";
                return RedirectToAction(nameof(Dashboard));
            }

            var result = await _apiClient.PostAsync<PlayerCreateDto, Player>($"api/teams/{team.TeamId}/players", dto);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Không thể thêm cầu thủ vào đội.";
            }
            else
            {
                TempData["Success"] = $"Thêm cầu thủ {dto.FullName} (Số áo: {dto.JerseyNumber}) thành công!";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> DeletePlayer(int id)
        {
            var result = await _apiClient.DeleteAsync($"api/players/{id}");
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Không thể xóa cầu thủ.";
            }
            else
            {
                TempData["Success"] = "Đã xóa cầu thủ khỏi danh sách đội bóng.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> RegisterTournament(int tournamentId)
        {
            var result = await _apiClient.PostAsync<object, object>($"api/tournaments/{tournamentId}/register", new { });
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage ?? "Không thể gửi hồ sơ đăng ký tham gia giải đấu.";
            }
            else
            {
                TempData["Success"] = "Gửi hồ sơ đăng ký thành công! Vui lòng chờ Ban tổ chức xem xét và phê duyệt.";
            }

            return RedirectToAction(nameof(Dashboard));
        }
    }
}
