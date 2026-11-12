using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.BusinessLogic.Services;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TournamentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITournamentService _tournamentService;
        private readonly IKnockoutService _knockoutService;

        public TournamentsController(AppDbContext context, ITournamentService tournamentService, IKnockoutService knockoutService)
        {
            _context = context;
            _tournamentService = tournamentService;
            _knockoutService = knockoutService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status)
        {
            var query = _context.Tournaments
                .Include(t => t.Registrations)
                .Include(t => t.ChampionTeam)
                .Include(t => t.Matches)
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
                    GroupCount = t.GroupCount,
                    KnockoutTeamCount = t.KnockoutTeamCount,
                    HasThirdPlaceMatch = t.HasThirdPlaceMatch,
                    ChampionTeamId = t.ChampionTeamId,
                    ChampionTeamName = t.ChampionTeam != null ? t.ChampionTeam.TeamName : null,
                    HasKnockoutBracket = t.Matches.Any(m => m.Stage == "Knockout" && m.Status != "Cancelled"),
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
                GroupCount = dto.GroupCount,
                KnockoutTeamCount = dto.KnockoutTeamCount,
                HasThirdPlaceMatch = dto.HasThirdPlaceMatch,
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
            tournament.GroupCount = dto.GroupCount;
            tournament.KnockoutTeamCount = dto.KnockoutTeamCount;
            tournament.HasThirdPlaceMatch = dto.HasThirdPlaceMatch;
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
            var result = await _tournamentService.DeleteOrCancelTournamentAsync(id);
            if (!result.IsSuccess)
            {
                return StatusCode(result.StatusCode, new { message = result.Message });
            }

            return Ok(new { message = result.Message });
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
                .Include(t => t.Players.Where(p => p.IsActive))
                .FirstOrDefaultAsync(t => t.ManagerId == userId);

            if (team == null)
            {
                return BadRequest(new { message = "Bạn chưa tạo đội bóng. Hãy tạo đội bóng trước khi đăng ký giải." });
            }

            // Kiểm tra điều kiện tối thiểu cầu thủ đang hoạt động
            var activePlayers = team.Players.Where(p => p.IsActive).ToList();
            if (activePlayers.Count < tournament.MinPlayersPerTeam)
            {
                return BadRequest(new { message = $"Đội bóng của bạn hiện có {activePlayers.Count} cầu thủ. Giải đấu yêu cầu tối thiểu {tournament.MinPlayersPerTeam} cầu thủ." });
            }

            var hasGk = activePlayers.Any(p => p.Position == "GK");
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

        [HttpGet("{id}/knockout/setup")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetKnockoutSetup(int id)
        {
            var result = await _knockoutService.GetKnockoutSetupAsync(id);
            if (!result.IsSuccess)
            {
                return NotFound(new { message = result.ErrorMessage });
            }
            return Ok(result.Data);
        }

        [HttpPost("{id}/knockout/generate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateKnockoutBracket(int id, [FromBody] GenerateKnockoutBracketDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _knockoutService.GenerateBracketAsync(id, dto);
            if (!result.IsSuccess)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(result.Bracket);
        }

        [HttpGet("{id}/knockout/bracket")]
        public async Task<IActionResult> GetKnockoutBracket(int id)
        {
            var bracket = await _knockoutService.GetBracketAsync(id);
            if (bracket == null)
            {
                return NotFound(new { message = "Không tìm thấy giải đấu." });
            }
            return Ok(bracket);
        }

        [HttpDelete("{id}/knockout")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetKnockoutBracket(int id)
        {
            var result = await _knockoutService.ResetBracketAsync(id);
            if (!result.IsSuccess)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }
            return Ok(new { message = "Đã xóa nhánh đấu Knockout thành công. Bạn có thể sắp xếp lại cặp đấu mới." });
        }
    }
}
