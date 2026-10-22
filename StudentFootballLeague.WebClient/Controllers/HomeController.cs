using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.WebClient.Services;

namespace StudentFootballLeague.WebClient.Controllers
{
    public class HomeController : Controller
    {
        private readonly IApiClientService _apiClient;

        public HomeController(IApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Lấy danh sách mùa giải
            var tournaments = await _apiClient.GetAsync<List<TournamentReadDto>>("api/tournaments") ?? new List<TournamentReadDto>();
            var activeTournament = tournaments.FirstOrDefault(t => t.Status == "Ongoing") ?? tournaments.FirstOrDefault();

            // 2. Lấy danh sách trận đấu gần đây
            var matches = await _apiClient.GetAsync<List<MatchReadDto>>("api/matches") ?? new List<MatchReadDto>();

            // 3. Lấy Top 5 Vua phá lưới
            List<TopScorerDto> topScorers = new List<TopScorerDto>();
            if (activeTournament != null)
            {
                topScorers = await _apiClient.GetAsync<List<TopScorerDto>>($"api/tournaments/{activeTournament.TournamentId}/top-scorers?top=5") ?? new List<TopScorerDto>();
            }

            ViewBag.ActiveTournament = activeTournament;
            ViewBag.Tournaments = tournaments;
            ViewBag.Matches = matches.Take(6).ToList();
            ViewBag.TopScorers = topScorers;

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
