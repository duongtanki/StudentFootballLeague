using System.Linq;
using System.Security.Claims;
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
    public class PlayersController : ODataController
    {
        private readonly AppDbContext _context;
        private readonly IPlayerService _playerService;

        public PlayersController(AppDbContext context, IPlayerService playerService)
        {
            _context = context;
            _playerService = playerService;
        }

        [HttpGet("odata/Players")]
        [HttpGet("api/players")]
        [EnableQuery(MaxTop = 100)]
        public IQueryable<Player> Get()
        {
            return _context.Players
                .Where(p => p.IsActive)
                .Include(p => p.Team)
                .AsQueryable();
        }

        [HttpGet("api/teams/{teamId}/players")]
        public async Task<IActionResult> GetByTeamId(int teamId)
        {
            var players = await _context.Players
                .Where(p => p.TeamId == teamId && p.IsActive)
                .OrderBy(p => p.JerseyNumber)
                .Select(p => new PlayerReadDto
                {
                    PlayerId = p.PlayerId,
                    TeamId = p.TeamId,
                    TeamName = p.Team != null ? p.Team.TeamName : "",
                    StudentCode = p.StudentCode,
                    FullName = p.FullName,
                    DateOfBirth = p.DateOfBirth,
                    JerseyNumber = p.JerseyNumber,
                    Position = p.Position,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return Ok(players);
        }

        [HttpPost("api/teams/{teamId}/players")]
        [Authorize(Roles = "TeamManager,Admin")]
        public async Task<IActionResult> AddPlayer(int teamId, [FromBody] PlayerCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                return Unauthorized();
            }
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

            var result = await _playerService.AddPlayerAsync(teamId, userId, userRole, dto);
            if (!result.IsSuccess || result.Player == null)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }

            var p = result.Player;
            return Ok(new PlayerReadDto
            {
                PlayerId = p.PlayerId,
                TeamId = p.TeamId,
                StudentCode = p.StudentCode,
                FullName = p.FullName,
                DateOfBirth = p.DateOfBirth,
                JerseyNumber = p.JerseyNumber,
                Position = p.Position,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            });
        }

        [HttpPut("api/players/{id}")]
        [Authorize(Roles = "TeamManager,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] PlayerUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                return Unauthorized();
            }
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

            var result = await _playerService.UpdatePlayerAsync(id, userId, userRole, dto);
            if (!result.IsSuccess || result.Player == null)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }

            var p = result.Player;
            return Ok(new PlayerReadDto
            {
                PlayerId = p.PlayerId,
                TeamId = p.TeamId,
                TeamName = p.Team?.TeamName ?? "",
                StudentCode = p.StudentCode,
                FullName = p.FullName,
                DateOfBirth = p.DateOfBirth,
                JerseyNumber = p.JerseyNumber,
                Position = p.Position,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            });
        }

        [HttpDelete("api/players/{id}")]
        [Authorize(Roles = "TeamManager,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                return Unauthorized();
            }
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

            var result = await _playerService.SoftDeletePlayerAsync(id, userId, userRole);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
            }

            return Ok(new { message = "Đã xóa mềm cầu thủ thành công (bảo toàn lịch sử thi đấu)." });
        }
    }
}
