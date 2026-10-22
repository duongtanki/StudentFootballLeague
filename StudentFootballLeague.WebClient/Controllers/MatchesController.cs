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

        public async Task<IActionResult> Index(int? tournamentId, string? groupName, string? roundName, string? searchTeam)
        {
            var tournaments = await _apiClient.GetAsync<List<TournamentReadDto>>("api/tournaments") ?? new List<TournamentReadDto>();
            var matches = await _apiClient.GetAsync<List<MatchReadDto>>("api/matches") ?? new List<MatchReadDto>();

            if (tournamentId.HasValue)
            {
                matches = matches.Where(m => m.TournamentId == tournamentId.Value).ToList();
            }

            if (!string.IsNullOrEmpty(groupName))
            {
                matches = matches.Where(m => string.Equals(m.GroupName, groupName, System.StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(roundName))
            {
                matches = matches.Where(m => m.RoundName.Contains(roundName, System.StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(searchTeam))
            {
                matches = matches.Where(m => m.HomeTeamName.Contains(searchTeam, System.StringComparison.OrdinalIgnoreCase) ||
                                             m.AwayTeamName.Contains(searchTeam, System.StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewBag.Tournaments = tournaments;
            ViewBag.SelectedTournament = tournamentId;
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

            // Lấy danh sách cầu thủ 2 đội
            var homePlayers = await _apiClient.GetAsync<List<PlayerReadDto>>($"api/teams/{match.HomeTeamId}/players") ?? new List<PlayerReadDto>();
            var awayPlayers = await _apiClient.GetAsync<List<PlayerReadDto>>($"api/teams/{match.AwayTeamId}/players") ?? new List<PlayerReadDto>();

            ViewBag.HomePlayers = homePlayers;
            ViewBag.AwayPlayers = awayPlayers;

            return View(match);
        }
    }
}
