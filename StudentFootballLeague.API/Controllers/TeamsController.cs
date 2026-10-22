using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.API.Controllers
{
    public class TeamsController : ODataController
    {
        private readonly AppDbContext _context;

        public TeamsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("odata/Teams")]
        [HttpGet("api/teams")]
        [EnableQuery(MaxTop = 100)]
        public IQueryable<Team> Get()
        {
            return _context.Teams
                .Include(t => t.Manager)
                .Include(t => t.Players)
                .AsQueryable();
        }

        [HttpGet("api/teams/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var team = await _context.Teams
                .Include(t => t.Manager)
                .Include(t => t.Players)
                .FirstOrDefaultAsync(t => t.TeamId == id);

            if (team == null)
            {
                return NotFound(new { message = "Không tìm thấy đội bóng." });
            }

            return Ok(new
            {
                team.TeamId,
                team.TeamName,
                team.FacultyOrClub,
                team.LogoUrl,
                team.ManagerId,
                ManagerName = team.Manager != null ? team.Manager.FullName : "",
                ManagerEmail = team.Manager != null ? team.Manager.Email : "",
                team.HomeKitColor,
                team.CreatedAt,
                Players = team.Players.Select(p => new
                {
                    p.PlayerId,
                    p.StudentCode,
                    p.FullName,
                    p.DateOfBirth,
                    p.JerseyNumber,
                    p.Position,
                    p.AvatarUrl,
                    p.IsActive
                })
            });
        }

        [HttpGet("api/teams/my-team")]
        [Authorize(Roles = "TeamManager")]
        public async Task<IActionResult> GetMyTeam()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var team = await _context.Teams
                .Include(t => t.Players)
                .Include(t => t.Registrations)
                    .ThenInclude(r => r.Tournament)
                .FirstOrDefaultAsync(t => t.ManagerId == userId);

            if (team == null)
            {
                return NotFound(new { message = "Bạn chưa có hồ sơ đội bóng nào." });
            }

            return Ok(team);
        }

        [HttpPost("api/teams")]
        [Authorize(Roles = "TeamManager")]
        public async Task<IActionResult> Create([FromBody] TeamCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var hasTeam = await _context.Teams.AnyAsync(t => t.ManagerId == userId);
            if (hasTeam)
            {
                return BadRequest(new { message = "Bạn đã quản lý một đội bóng rồi. Mỗi đội trưởng chỉ được quản lý 1 đội." });
            }

            var nameExists = await _context.Teams.AnyAsync(t => t.TeamName.ToLower() == dto.TeamName.ToLower());
            if (nameExists)
            {
                return BadRequest(new { message = "Tên đội bóng này đã được đặt, vui lòng chọn tên khác." });
            }

            var team = new Team
            {
                TeamName = dto.TeamName,
                FacultyOrClub = dto.FacultyOrClub,
                LogoUrl = dto.LogoUrl,
                ManagerId = userId,
                HomeKitColor = dto.HomeKitColor
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = team.TeamId }, team);
        }

        [HttpPut("api/teams/{id}")]
        [Authorize(Roles = "TeamManager,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] TeamUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var team = await _context.Teams.FindAsync(id);
            if (team == null)
            {
                return NotFound(new { message = "Không tìm thấy đội bóng." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userRole != "Admin" && team.ManagerId.ToString() != userIdClaim)
            {
                return Forbid();
            }

            team.TeamName = dto.TeamName;
            team.FacultyOrClub = dto.FacultyOrClub;
            if (!string.IsNullOrEmpty(dto.LogoUrl))
            {
                team.LogoUrl = dto.LogoUrl;
            }
            team.HomeKitColor = dto.HomeKitColor;

            await _context.SaveChangesAsync();

            return Ok(team);
        }
    }
}
