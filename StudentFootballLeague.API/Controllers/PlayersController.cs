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
    public class PlayersController : ODataController
    {
        private readonly AppDbContext _context;

        public PlayersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("odata/Players")]
        [HttpGet("api/players")]
        [EnableQuery(MaxTop = 100)]
        public IQueryable<Player> Get()
        {
            return _context.Players
                .Include(p => p.Team)
                .AsQueryable();
        }

        [HttpGet("api/teams/{teamId}/players")]
        public async Task<IActionResult> GetByTeamId(int teamId)
        {
            var players = await _context.Players
                .Where(p => p.TeamId == teamId)
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
                    AvatarUrl = p.AvatarUrl,
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

            var team = await _context.Teams.FindAsync(teamId);
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

            // 1. Kiểm tra xem MSSV đã thuộc về cầu thủ nào khác chưa
            var existingCode = await _context.Players.AnyAsync(p => p.StudentCode == dto.StudentCode);
            if (existingCode)
            {
                return BadRequest(new { message = $"Cầu thủ có MSSV '{dto.StudentCode}' đã được đăng ký trong hệ thống." });
            }

            // 2. Kiểm tra xem số áo trong đội đã bị trùng chưa
            var existingNumber = await _context.Players.AnyAsync(p => p.TeamId == teamId && p.JerseyNumber == dto.JerseyNumber);
            if (existingNumber)
            {
                return BadRequest(new { message = $"Số áo {dto.JerseyNumber} đã được cầu thủ khác trong đội sử dụng." });
            }

            var player = new Player
            {
                TeamId = teamId,
                StudentCode = dto.StudentCode,
                FullName = dto.FullName,
                DateOfBirth = dto.DateOfBirth,
                JerseyNumber = dto.JerseyNumber,
                Position = dto.Position,
                AvatarUrl = dto.AvatarUrl,
                IsActive = true
            };

            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            return Ok(player);
        }

        [HttpPut("api/players/{id}")]
        [Authorize(Roles = "TeamManager,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] PlayerUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var player = await _context.Players
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.PlayerId == id);

            if (player == null)
            {
                return NotFound(new { message = "Không tìm thấy cầu thủ." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userRole != "Admin" && player.Team != null && player.Team.ManagerId.ToString() != userIdClaim)
            {
                return Forbid();
            }

            // Kiểm tra số áo trùng lặp nếu sửa số áo
            if (player.JerseyNumber != dto.JerseyNumber)
            {
                var existingNumber = await _context.Players
                    .AnyAsync(p => p.TeamId == player.TeamId && p.JerseyNumber == dto.JerseyNumber && p.PlayerId != id);
                if (existingNumber)
                {
                    return BadRequest(new { message = $"Số áo {dto.JerseyNumber} đã được cầu thủ khác sử dụng." });
                }
            }

            player.FullName = dto.FullName;
            player.DateOfBirth = dto.DateOfBirth;
            player.JerseyNumber = dto.JerseyNumber;
            player.Position = dto.Position;
            player.IsActive = dto.IsActive;
            if (!string.IsNullOrEmpty(dto.AvatarUrl))
            {
                player.AvatarUrl = dto.AvatarUrl;
            }

            await _context.SaveChangesAsync();

            return Ok(player);
        }

        [HttpDelete("api/players/{id}")]
        [Authorize(Roles = "TeamManager,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var player = await _context.Players
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.PlayerId == id);

            if (player == null)
            {
                return NotFound(new { message = "Không tìm thấy cầu thủ." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userRole != "Admin" && player.Team != null && player.Team.ManagerId.ToString() != userIdClaim)
            {
                return Forbid();
            }

            _context.Players.Remove(player);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa cầu thủ thành công." });
        }
    }
}
