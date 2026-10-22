using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.WebClient.Services;

namespace StudentFootballLeague.WebClient.Controllers
{
    public class StandingsController : Controller
    {
        private readonly IApiClientService _apiClient;

        public StandingsController(IApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Index(int? tournamentId)
        {
            var tournaments = await _apiClient.GetAsync<List<TournamentReadDto>>("api/tournaments") ?? new List<TournamentReadDto>();
            var selectedTour = tournamentId.HasValue 
                ? tournaments.FirstOrDefault(t => t.TournamentId == tournamentId.Value) 
                : (tournaments.FirstOrDefault(t => t.Status == "Ongoing") ?? tournaments.FirstOrDefault());

            List<StandingReadDto> groupA = new List<StandingReadDto>();
            List<StandingReadDto> groupB = new List<StandingReadDto>();
            List<TopScorerDto> topScorers = new List<TopScorerDto>();

            if (selectedTour != null)
            {
                groupA = await _apiClient.GetAsync<List<StandingReadDto>>($"api/tournaments/{selectedTour.TournamentId}/standings?groupName=Group A") ?? new List<StandingReadDto>();
                groupB = await _apiClient.GetAsync<List<StandingReadDto>>($"api/tournaments/{selectedTour.TournamentId}/standings?groupName=Group B") ?? new List<StandingReadDto>();
                topScorers = await _apiClient.GetAsync<List<TopScorerDto>>($"api/tournaments/{selectedTour.TournamentId}/top-scorers?top=10") ?? new List<TopScorerDto>();
            }

            ViewBag.Tournaments = tournaments;
            ViewBag.SelectedTournament = selectedTour;
            ViewBag.GroupA = groupA;
            ViewBag.GroupB = groupB;
            ViewBag.TopScorers = topScorers;

            return View();
        }
    }
}
