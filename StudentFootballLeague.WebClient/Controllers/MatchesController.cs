using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.WebClient.Services;

namespace StudentFootballLeague.WebClient.Controllers
{
    public class MatchesController : Controller
    {
        private readonly IApiClientService _apiClient;

        public MatchesController(IApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Index(int? tournamentId, string? status, string? groupName, string? roundName, string? searchTeam)
        {
            var tournaments = await _apiClient.GetAsync<List<TournamentReadDto>>("api/tournaments") ?? new List<TournamentReadDto>();
            var matches = await _apiClient.GetAsync<List<MatchReadDto>>("api/matches") ?? new List<MatchReadDto>();

            if (tournamentId.HasValue)
            {
                matches = matches.Where(m => m.TournamentId == tournamentId.Value).ToList();
            }

            // Danh sách bảng & vòng có sẵn trong giải đang chọn để hiển thị trên dropdown lọc
            var availableGroups = matches
                .Where(m => !string.IsNullOrWhiteSpace(m.GroupName))
                .Select(m => m.GroupName!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .ToList();

            var availableRounds = matches
                .Where(m => !string.IsNullOrWhiteSpace(m.RoundName))
                .Select(m => m.RoundName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrEmpty(status))
            {
                if (status.Equals("upcoming", StringComparison.OrdinalIgnoreCase))
                {
                    matches = matches.Where(m => m.Status == "Scheduled" || m.Status == "InProgress").ToList();
                }
                else if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    matches = matches.Where(m => m.Status == "Completed").ToList();
                }
            }

            if (!string.IsNullOrEmpty(groupName))
            {
                matches = matches.Where(m => string.Equals(m.GroupName, groupName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(roundName))
            {
                matches = matches.Where(m => string.Equals(m.RoundName, roundName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(searchTeam))
            {
                matches = matches.Where(m => m.HomeTeamName.Contains(searchTeam, StringComparison.OrdinalIgnoreCase) ||
                                             m.AwayTeamName.Contains(searchTeam, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Sắp xếp theo ngày giảm dần, trong cùng một ngày sắp xếp theo giờ tăng dần
            matches = matches
                .OrderByDescending(m => m.MatchDateTime.Date)
                .ThenBy(m => m.MatchDateTime)
                .ToList();

            ViewBag.Tournaments = tournaments;
            ViewBag.AvailableGroups = availableGroups;
            ViewBag.AvailableRounds = availableRounds;
            ViewBag.SelectedTournament = tournamentId;
            ViewBag.SelectedStatus = status ?? "all";
            ViewBag.SelectedGroup = groupName;
            ViewBag.SelectedRound = roundName;
            ViewBag.SearchTeam = searchTeam;

            return View(matches);
        }

        public async Task<IActionResult> Details(int id)
        {
            var match = await _apiClient.GetAsync<MatchReadDto>($"api/matches/{id}");
            if (match == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin trận đấu.";
                return RedirectToAction(nameof(Index));
            }

            // Lấy danh sách cầu thủ 2 đội (nếu đã xác định đội)
            var homePlayers = match.HomeTeamId.HasValue
                ? (await _apiClient.GetAsync<List<PlayerReadDto>>($"api/teams/{match.HomeTeamId.Value}/players") ?? new List<PlayerReadDto>())
                : new List<PlayerReadDto>();
            var awayPlayers = match.AwayTeamId.HasValue
                ? (await _apiClient.GetAsync<List<PlayerReadDto>>($"api/teams/{match.AwayTeamId.Value}/players") ?? new List<PlayerReadDto>())
                : new List<PlayerReadDto>();

            ViewBag.HomePlayers = homePlayers;
            ViewBag.AwayPlayers = awayPlayers;

            return View(match);
        }
    }
}
