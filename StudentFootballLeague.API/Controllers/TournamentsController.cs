using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TournamentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TournamentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status)
        {
            var query = _context.Tournaments
                .Include(t => t.Registrations)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status.ToLower() == status.ToLower());
            }

            var tournaments = await query
                .OrderByDescending(t => t.StartDate)
                .Select(t => new TournamentReadDto
                {
                    TournamentId = t.TournamentId,
                    Name = t.Name,
                    Season = t.Season,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    RegistrationDeadline = t.RegistrationDeadline,
                    MaxTeams = t.MaxTeams,
                    MinPlayersPerTeam = t.MinPlayersPerTeam,
                    MaxPlayersPerTeam = t.MaxPlayersPerTeam,
                    Status = t.Status,
                    RegisteredTeamsCount = t.Registrations.Count(r => r.ApprovalStatus == "Approved"),
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return Ok(tournaments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var t = await _context.Tournaments
                .Include(t => t.Registrations)
                    .ThenInclude(r => r.Team)
                .FirstOrDefaultAsync(t => t.TournamentId == id);

            if (t == null)
            {
                return NotFound(new { message = "Không tìm thấy mùa giải." });
            }

            return Ok(new
            {
                t.TournamentId,
                t.Name,
                t.Season,
                t.StartDate,
                t.EndDate,
                t.RegistrationDeadline,
                t.MaxTeams,
                t.MinPlayersPerTeam,
                t.MaxPlayersPerTeam,
                t.Status,
                t.CreatedAt,
                RegisteredTeams = t.Registrations.Select(r => new
                {
                    r.RegistrationId,
                    r.TeamId,
                    TeamName = r.Team != null ? r.Team.TeamName : "",
                    LogoUrl = r.Team != null ? r.Team.LogoUrl : null,
                    r.GroupName,
                    r.ApprovalStatus,
                    r.RejectReason,
                    r.Points
                })
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] TournamentCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var exists = await _context.Tournaments.AnyAsync(t => t.Name.ToLower() == dto.Name.ToLower());
            if (exists)
            {
                return BadRequest(new { message = "Tên giải đấu này đã tồn tại." });
            }

            var tournament = new Tournament
            {
                Name = dto.Name,
                Season = dto.Season,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                RegistrationDeadline = dto.RegistrationDeadline,
                MaxTeams = dto.MaxTeams,
                MinPlayersPerTeam = dto.MinPlayersPerTeam,
                MaxPlayersPerTeam = dto.MaxPlayersPerTeam,
                Status = dto.Status
            };

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = tournament.TournamentId }, tournament);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] TournamentUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tournament = await _context.Tournaments.FindAsync(id);
            if (tournament == null)
            {
                return NotFound(new { message = "Không tìm thấy giải đấu." });
            }

            tournament.Name = dto.Name;
            tournament.Season = dto.Season;
            tournament.StartDate = dto.StartDate;
            tournament.EndDate = dto.EndDate;
            tournament.RegistrationDeadline = dto.RegistrationDeadline;
            tournament.MaxTeams = dto.MaxTeams;
            tournament.MinPlayersPerTeam = dto.MinPlayersPerTeam;
            tournament.MaxPlayersPerTeam = dto.MaxPlayersPerTeam;
            tournament.Status = dto.Status;

            await _context.SaveChangesAsync();

            return Ok(tournament);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.Matches)
                .FirstOrDefaultAsync(t => t.TournamentId == id);

            if (tournament == null)
            {
                return NotFound(new { message = "Không tìm thấy giải đấu." });
            }

            if (tournament.Matches.Any(m => m.Status == "Completed" || m.Status == "InProgress"))
            {
                return BadRequest(new { message = "Không thể xóa giải đấu đã có trận đấu diễn ra." });
            }

            _context.Tournaments.Remove(tournament);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa giải đấu thành công." });
        }

        [HttpPost("{id}/register")]
        [Authorize(Roles = "TeamManager")]
        public async Task<IActionResult> RegisterTeam(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var tournament = await _context.Tournaments.FindAsync(id);
            if (tournament == null)
            {
                return NotFound(new { message = "Giải đấu không tồn tại." });
            }

            if (DateTime.UtcNow > tournament.RegistrationDeadline)
            {
                return BadRequest(new { message = "Giải đấu đã hết hạn đăng ký." });
            }

            var team = await _context.Teams
                .Include(t => t.Players)
                .FirstOrDefaultAsync(t => t.ManagerId == userId);

            if (team == null)
            {
                return BadRequest(new { message = "Bạn chưa tạo đội bóng. Hãy tạo đội bóng trước khi đăng ký giải." });
            }

            // Kiểm tra điều kiện tối thiểu cầu thủ
            if (team.Players.Count < tournament.MinPlayersPerTeam)
            {
                return BadRequest(new { message = $"Đội bóng của bạn hiện có {team.Players.Count} cầu thủ. Giải đấu yêu cầu tối thiểu {tournament.MinPlayersPerTeam} cầu thủ." });
            }

            var hasGk = team.Players.Any(p => p.Position == "GK");
            if (!hasGk)
            {
                return BadRequest(new { message = "Đội bóng phải có ít nhất 1 Thủ môn (GK) mới đủ điều kiện đăng ký." });
            }

            var existingReg = await _context.TeamRegistrations
                .FirstOrDefaultAsync(r => r.TournamentId == id && r.TeamId == team.TeamId);

            if (existingReg != null)
            {
                return BadRequest(new { message = "Đội bóng của bạn đã gửi đăng ký tham gia giải đấu này rồi." });
            }

            var reg = new TeamRegistration
            {
                TournamentId = id,
                TeamId = team.TeamId,
                ApprovalStatus = "Pending"
            };

            _context.TeamRegistrations.Add(reg);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Gửi hồ sơ đăng ký thành công, vui lòng chờ Ban tổ chức phê duyệt.", registrationId = reg.RegistrationId });
        }
    }
}
