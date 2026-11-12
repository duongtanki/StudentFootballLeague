using System;
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

            var groupStandings = new List<GroupStandingsBlockDto>();
            var topScorers = new List<TopScorerDto>();
            KnockoutBracketReadDto? bracket = null;

            if (selectedTour != null)
            {
                var groups = await _apiClient.GetAsync<List<string>>($"api/tournaments/{selectedTour.TournamentId}/groups") ?? new List<string>();
                if (!groups.Any())
                {
                    groups = new List<string> { "Group A", "Group B" };
                }

                foreach (var g in groups)
                {
                    var list = await _apiClient.GetAsync<List<StandingReadDto>>($"api/tournaments/{selectedTour.TournamentId}/standings?groupName={Uri.EscapeDataString(g)}") ?? new List<StandingReadDto>();
                    groupStandings.Add(new GroupStandingsBlockDto { GroupName = g, Standings = list });
                }

                topScorers = await _apiClient.GetAsync<List<TopScorerDto>>($"api/tournaments/{selectedTour.TournamentId}/top-scorers?top=10") ?? new List<TopScorerDto>();
                bracket = await _apiClient.GetAsync<KnockoutBracketReadDto>($"api/tournaments/{selectedTour.TournamentId}/knockout/bracket");
            }

            ViewBag.Tournaments = tournaments;
            ViewBag.SelectedTournament = selectedTour;
            ViewBag.GroupStandings = groupStandings;
            ViewBag.GroupA = groupStandings.FirstOrDefault(g => g.GroupName == "Group A")?.Standings ?? new List<StandingReadDto>();
            ViewBag.GroupB = groupStandings.FirstOrDefault(g => g.GroupName == "Group B")?.Standings ?? new List<StandingReadDto>();
            ViewBag.TopScorers = topScorers;
            ViewBag.KnockoutBracket = bracket;

            return View();
        }
    }
}
