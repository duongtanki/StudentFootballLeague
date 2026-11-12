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
        private readonly IStandingsService _standingsService;

        public MatchesController(AppDbContext context, IMatchService matchService, IStandingsService standingsService)
        {
            _context = context;
            _matchService = matchService;
            _standingsService = standingsService;
        }

        [HttpGet("odata/Matches")]
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

        [HttpGet("api/matches")]
        public async Task<IActionResult> GetAllMatches([FromQuery] int? tournamentId, [FromQuery] string? groupName, [FromQuery] string? status)
        {
            var query = _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.WinnerTeam)
                .Include(m => m.Referee)
                .Include(m => m.Tournament)
                .Include(m => m.Events)
                    .ThenInclude(e => e.Player)
                .AsQueryable();

            if (tournamentId.HasValue)
            {
                query = query.Where(m => m.TournamentId == tournamentId.Value);
            }

            if (!string.IsNullOrEmpty(groupName))
            {
                query = query.Where(m => m.GroupName == groupName);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(m => m.Status == status);
            }

            var matches = await query
                .OrderByDescending(m => m.MatchDateTime)
                .Select(m => new MatchReadDto
                {
                    MatchId = m.MatchId,
                    TournamentId = m.TournamentId,
                    TournamentName = m.Tournament != null ? m.Tournament.Name : "",
                    GroupName = m.GroupName,
                    RoundName = m.RoundName,
                    Stage = m.Stage,
                    KnockoutRound = m.KnockoutRound,
                    BranchName = m.BranchName,
                    BracketOrder = m.BracketOrder,
                    HomeSourceLabel = m.HomeSourceLabel,
                    AwaySourceLabel = m.AwaySourceLabel,
                    HomeTeamId = m.HomeTeamId,
                    HomeTeamName = m.HomeTeam != null ? m.HomeTeam.TeamName : (m.HomeSourceLabel ?? "Chờ xác định"),
                    AwayTeamId = m.AwayTeamId,
                    AwayTeamName = m.AwayTeam != null ? m.AwayTeam.TeamName : (m.AwaySourceLabel ?? "Chờ xác định"),
                    WinnerTeamId = m.WinnerTeamId,
                    WinnerTeamName = m.WinnerTeam != null ? m.WinnerTeam.TeamName : null,
                    NextMatchId = m.NextMatchId,
                    NextMatchSlot = m.NextMatchSlot,
                    LoserNextMatchId = m.LoserNextMatchId,
                    LoserNextMatchSlot = m.LoserNextMatchSlot,
                    MatchDateTime = m.MatchDateTime,
                    PitchName = m.PitchName,
                    RefereeId = m.RefereeId,
                    RefereeName = m.Referee != null ? m.Referee.FullName : null,
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
                        TeamName = e.TeamId == m.HomeTeamId ? (m.HomeTeam != null ? m.HomeTeam.TeamName : "") : (m.AwayTeam != null ? m.AwayTeam.TeamName : ""),
                        PlayerId = e.PlayerId,
                        PlayerName = e.Player != null ? e.Player.FullName : "",
                        JerseyNumber = e.Player != null ? e.Player.JerseyNumber : 0,
                        EventType = e.EventType,
                        Minute = e.Minute,
                        Description = e.Description
                    }).OrderBy(e => e.Minute).ToList()
                })
                .ToListAsync();

            return Ok(matches);
        }

        [HttpGet("api/matches/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var m = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.WinnerTeam)
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
                Stage = m.Stage,
                KnockoutRound = m.KnockoutRound,
                BranchName = m.BranchName,
                BracketOrder = m.BracketOrder,
                HomeSourceLabel = m.HomeSourceLabel,
                AwaySourceLabel = m.AwaySourceLabel,
                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam?.TeamName ?? m.HomeSourceLabel ?? "Chờ xác định",
                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam?.TeamName ?? m.AwaySourceLabel ?? "Chờ xác định",
                WinnerTeamId = m.WinnerTeamId,
                WinnerTeamName = m.WinnerTeam?.TeamName,
                NextMatchId = m.NextMatchId,
                NextMatchSlot = m.NextMatchSlot,
                LoserNextMatchId = m.LoserNextMatchId,
                LoserNextMatchSlot = m.LoserNextMatchSlot,
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

        [HttpGet("api/matches/referees")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetReferees()
        {
            var referees = await _context.Users
                .Where(u => u.Role == "Referee" && u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.UserId, u.FullName, u.Email })
                .ToListAsync();

            return Ok(referees);
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

        [HttpPut("api/matches/{id}/schedule")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateSchedule(int id, [FromBody] MatchScheduleUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _matchService.UpdateMatchScheduleAsync(id, dto);
            if (!result.IsSuccess)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(new { message = "Cập nhật lịch thi đấu thành công." });
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

        [HttpDelete("api/matches/{matchId}/events/{eventId}")]
        [Authorize(Roles = "Admin,Referee")]
        public async Task<IActionResult> DeleteMatchEvent(int matchId, int eventId)
        {
            var ev = await _context.MatchEvents
                .Include(e => e.Match)
                .FirstOrDefaultAsync(e => e.MatchId == matchId && e.EventId == eventId);

            if (ev == null)
            {
                return NotFound(new { message = "Không tìm thấy sự kiện trận đấu." });
            }

            var match = ev.Match;
            if (match == null)
            {
                return NotFound(new { message = "Không tìm thấy trận đấu liên quan." });
            }

            // Nếu là bàn thắng, tự động giảm tỷ số tương ứng
            if (ev.EventType == "Goal" || ev.EventType == "PenaltyGoal")
            {
                if (ev.TeamId == match.HomeTeamId && (match.HomeScore ?? 0) > 0)
                {
                    match.HomeScore--;
                }
                else if (ev.TeamId == match.AwayTeamId && (match.AwayScore ?? 0) > 0)
                {
                    match.AwayScore--;
                }
            }

            _context.MatchEvents.Remove(ev);
            await _context.SaveChangesAsync();

            // Chỉ tự động tính lại Bảng xếp hạng đối với trận vòng bảng
            if (match.Stage == "Group")
            {
                await _standingsService.RecalculateStandingsAsync(match.TournamentId, match.GroupName);
            }

            return Ok(new { message = "Đã xóa sự kiện thành công. Tỷ số và thống kê đã được cập nhật lại." });
        }
    }
}
