using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.BusinessLogic.Services;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.API.Controllers
{
    public class MatchesController : ODataController
    {
        private readonly AppDbContext _context;
        private readonly IMatchService _matchService;

        public MatchesController(AppDbContext context, IMatchService matchService)
        {
            _context = context;
            _matchService = matchService;
        }

        [HttpGet("odata/Matches")]
        [HttpGet("api/matches")]
        [EnableQuery(MaxTop = 100)]
        public IQueryable<Match> Get()
        {
            return _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Referee)
                .Include(m => m.Tournament)
                .Include(m => m.Events)
                    .ThenInclude(e => e.Player)
                .AsQueryable();
        }

        [HttpGet("api/matches/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var m = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Referee)
                .Include(m => m.Tournament)
                .Include(m => m.Events)
                    .ThenInclude(e => e.Player)
                .FirstOrDefaultAsync(m => m.MatchId == id);

            if (m == null)
            {
                return NotFound(new { message = "Không tìm thấy trận đấu." });
            }

            var dto = new MatchReadDto
            {
                MatchId = m.MatchId,
                TournamentId = m.TournamentId,
                TournamentName = m.Tournament?.Name ?? "",
                GroupName = m.GroupName,
                RoundName = m.RoundName,
                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam?.TeamName ?? "",
                HomeTeamLogo = m.HomeTeam?.LogoUrl,
                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam?.TeamName ?? "",
                AwayTeamLogo = m.AwayTeam?.LogoUrl,
                MatchDateTime = m.MatchDateTime,
                PitchName = m.PitchName,
                RefereeId = m.RefereeId,
                RefereeName = m.Referee?.FullName,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,
                HomePenalty = m.HomePenalty,
                AwayPenalty = m.AwayPenalty,
                Status = m.Status,
                Events = m.Events.Select(e => new MatchEventReadDto
                {
                    EventId = e.EventId,
                    MatchId = e.MatchId,
                    TeamId = e.TeamId,
                    TeamName = e.TeamId == m.HomeTeamId ? (m.HomeTeam?.TeamName ?? "") : (m.AwayTeam?.TeamName ?? ""),
                    PlayerId = e.PlayerId,
                    PlayerName = e.Player?.FullName ?? "",
                    JerseyNumber = e.Player?.JerseyNumber ?? 0,
                    EventType = e.EventType,
                    Minute = e.Minute,
                    Description = e.Description
                }).OrderBy(e => e.Minute).ToList()
            };

            return Ok(dto);
        }

        [HttpPost("api/matches")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ScheduleMatch([FromBody] MatchScheduleDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _matchService.ScheduleMatchAsync(dto);
            if (!result.IsSuccess)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Match?.MatchId }, result.Match);
        }

        [HttpPut("api/matches/{id}/result")]
        [Authorize(Roles = "Admin,Referee")]
        public async Task<IActionResult> UpdateResult(int id, [FromBody] MatchResultUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _matchService.UpdateMatchResultAsync(id, dto);
            if (!result.IsSuccess)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(new { message = "Cập nhật tỷ số và sự kiện trận đấu thành công. Bảng xếp hạng đã được tính toán lại." });
        }
    }
}
