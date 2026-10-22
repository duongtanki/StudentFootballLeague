using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.DataAccess.Data;

namespace StudentFootballLeague.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RegistrationsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetRegistrations([FromQuery] int? tournamentId)
        {
            var query = _context.TeamRegistrations
                .Include(r => r.Team)
                .Include(r => r.Tournament)
                .AsQueryable();

            if (tournamentId.HasValue)
            {
                query = query.Where(r => r.TournamentId == tournamentId.Value);
            }

            var list = await query
                .OrderByDescending(r => r.RegisteredAt)
                .Select(r => new TeamRegistrationReadDto
                {
                    RegistrationId = r.RegistrationId,
                    TournamentId = r.TournamentId,
                    TournamentName = r.Tournament != null ? r.Tournament.Name : "",
                    TeamId = r.TeamId,
                    TeamName = r.Team != null ? r.Team.TeamName : "",
                    LogoUrl = r.Team != null ? r.Team.LogoUrl : null,
                    GroupName = r.GroupName,
                    ApprovalStatus = r.ApprovalStatus,
                    RejectReason = r.RejectReason,
                    MatchesPlayed = r.MatchesPlayed,
                    Wins = r.Wins,
                    Draws = r.Draws,
                    Losses = r.Losses,
                    GoalsFor = r.GoalsFor,
                    GoalsAgainst = r.GoalsAgainst,
                    Points = r.Points,
                    FairPlayPoints = r.FairPlayPoints,
                    RegisteredAt = r.RegisteredAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPut("{id}/approval")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveRegistration(int id, [FromBody] RegistrationApprovalDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var reg = await _context.TeamRegistrations
                .Include(r => r.Tournament)
                .Include(r => r.Team)
                    .ThenInclude(t => t.Players)
                .FirstOrDefaultAsync(r => r.RegistrationId == id);

            if (reg == null)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ đăng ký." });
            }

            if (dto.Status == "Approved")
            {
                if (reg.Team != null && reg.Tournament != null)
                {
                    if (reg.Team.Players.Count < reg.Tournament.MinPlayersPerTeam)
                    {
                        return BadRequest(new { message = $"Đội bóng không đủ điều kiện duyệt: cần tối thiểu {reg.Tournament.MinPlayersPerTeam} cầu thủ, hiện chỉ có {reg.Team.Players.Count} cầu thủ." });
                    }

                    if (!reg.Team.Players.Any(p => p.Position == "GK"))
                    {
                        return BadRequest(new { message = "Đội bóng không đủ điều kiện duyệt: chưa có Thủ môn (GK)." });
                    }
                }

                reg.ApprovalStatus = "Approved";
                reg.GroupName = string.IsNullOrEmpty(dto.GroupName) ? "Group A" : dto.GroupName;
                reg.RejectReason = null;
            }
            else
            {
                reg.ApprovalStatus = "Rejected";
                reg.RejectReason = dto.RejectReason ?? "Không đạt điều kiện xét duyệt của Ban tổ chức.";
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã cập nhật trạng thái hồ sơ thành '{reg.ApprovalStatus}'.", registration = reg });
        }
    }
}
