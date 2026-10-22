using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using StudentFootballLeague.BusinessLogic.Services;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.API.Controllers
{
    public class StandingsController : ODataController
    {
        private readonly AppDbContext _context;
        private readonly IStandingsService _standingsService;

        public StandingsController(AppDbContext context, IStandingsService standingsService)
        {
            _context = context;
            _standingsService = standingsService;
        }

        [HttpGet("odata/PlayerStats")]
        [EnableQuery(MaxTop = 100)]
        public IQueryable<PlayerStat> GetPlayerStats()
        {
            return _context.PlayerStats.AsQueryable();
        }

        [HttpGet("api/tournaments/{tournamentId}/standings")]
        public async Task<IActionResult> GetStandings(int tournamentId, [FromQuery] string? groupName)
        {
            var standings = await _standingsService.GetStandingsAsync(tournamentId, groupName);
            return Ok(standings);
        }

        [HttpGet("api/tournaments/{tournamentId}/top-scorers")]
        public async Task<IActionResult> GetTopScorers(int tournamentId, [FromQuery] int top = 10)
        {
            var scorers = await _standingsService.GetTopScorersAsync(tournamentId, top);
            return Ok(scorers);
        }

        [HttpPost("api/tournaments/{tournamentId}/recalculate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Recalculate(int tournamentId, [FromQuery] string? groupName)
        {
            await _standingsService.RecalculateStandingsAsync(tournamentId, groupName);
            return Ok(new { message = "Đã tính toán lại Bảng xếp hạng thành công." });
        }
    }
}
