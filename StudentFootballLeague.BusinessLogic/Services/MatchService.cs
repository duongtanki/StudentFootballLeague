using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.BusinessLogic.Services
{
    public interface IMatchService
    {
        Task<(bool IsSuccess, string? ErrorMessage, Match? Match)> ScheduleMatchAsync(MatchScheduleDto dto);
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateMatchResultAsync(int matchId, MatchResultUpdateDto dto);
    }

    public class MatchService : IMatchService
    {
        private readonly AppDbContext _context;
        private readonly IStandingsService _standingsService;

        public MatchService(AppDbContext context, IStandingsService standingsService)
        {
            _context = context;
            _standingsService = standingsService;
        }

        public async Task<(bool IsSuccess, string? ErrorMessage, Match? Match)> ScheduleMatchAsync(MatchScheduleDto dto)
        {
            if (dto.HomeTeamId == dto.AwayTeamId)
            {
                return (false, "Đội nhà và Đội khách không được trùng nhau.", null);
            }

            // 1. Kiểm tra giải đấu và 2 đội bóng có hợp lệ không
            var tournament = await _context.Tournaments.FindAsync(dto.TournamentId);
            if (tournament == null)
            {
                return (false, "Giải đấu không tồn tại.", null);
            }

            var homeReg = await _context.TeamRegistrations
                .FirstOrDefaultAsync(r => r.TournamentId == dto.TournamentId && r.TeamId == dto.HomeTeamId && r.ApprovalStatus == "Approved");
            var awayReg = await _context.TeamRegistrations
                .FirstOrDefaultAsync(r => r.TournamentId == dto.TournamentId && r.TeamId == dto.AwayTeamId && r.ApprovalStatus == "Approved");

            if (homeReg == null || awayReg == null)
            {
                return (false, "Một trong hai đội chưa được duyệt hồ sơ tham gia giải đấu này.", null);
            }

            // 2. Kiểm tra xung đột lịch thi đấu trong khoảng +- 90 phút
            var windowStart = dto.MatchDateTime.AddMinutes(-90);
            var windowEnd = dto.MatchDateTime.AddMinutes(90);

            var conflicts = await _context.Matches
                .Where(m => m.Status != "Cancelled" && m.MatchDateTime >= windowStart && m.MatchDateTime <= windowEnd)
                .ToListAsync();

            foreach (var existing in conflicts)
            {
                if (string.Equals(existing.PitchName, dto.PitchName, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Sân '{dto.PitchName}' đã có trận đấu diễn ra vào lúc {existing.MatchDateTime:HH:mm dd/MM/yyyy} (cần cách nhau ít nhất 90 phút).", null);
                }

                if (existing.HomeTeamId == dto.HomeTeamId || existing.AwayTeamId == dto.HomeTeamId)
                {
                    return (false, $"Đội nhà đang có lịch thi đấu lúc {existing.MatchDateTime:HH:mm dd/MM/yyyy} (cần nghỉ tối thiểu 90 phút).", null);
                }

                if (existing.HomeTeamId == dto.AwayTeamId || existing.AwayTeamId == dto.AwayTeamId)
                {
                    return (false, $"Đội khách đang có lịch thi đấu lúc {existing.MatchDateTime:HH:mm dd/MM/yyyy} (cần nghỉ tối thiểu 90 phút).", null);
                }
            }

            var match = new Match
            {
                TournamentId = dto.TournamentId,
                GroupName = dto.GroupName,
                RoundName = dto.RoundName,
                HomeTeamId = dto.HomeTeamId,
                AwayTeamId = dto.AwayTeamId,
                MatchDateTime = dto.MatchDateTime,
                PitchName = dto.PitchName,
                RefereeId = dto.RefereeId,
                Status = "Scheduled"
            };

            _context.Matches.Add(match);
            await _context.SaveChangesAsync();

            return (true, null, match);
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateMatchResultAsync(int matchId, MatchResultUpdateDto dto)
        {
            var match = await _context.Matches
                .Include(m => m.Events)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);

            if (match == null)
            {
                return (false, "Trận đấu không tồn tại.");
            }

            match.HomeScore = dto.HomeScore;
            match.AwayScore = dto.AwayScore;
            match.HomePenalty = dto.HomePenalty;
            match.AwayPenalty = dto.AwayPenalty;
            match.Status = dto.Status;

            // Xóa các sự kiện cũ nếu có để ghi lại chính xác
            _context.MatchEvents.RemoveRange(match.Events);

            if (dto.Events != null && dto.Events.Count > 0)
            {
                foreach (var ev in dto.Events)
                {
                    match.Events.Add(new MatchEvent
                    {
                        MatchId = matchId,
                        TeamId = ev.TeamId,
                        PlayerId = ev.PlayerId,
                        EventType = ev.EventType,
                        Minute = ev.Minute,
                        Description = ev.Description
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Tự động tính toán lại bảng xếp hạng
            await _standingsService.RecalculateStandingsAsync(match.TournamentId, match.GroupName);

            return (true, null);
        }
    }
}
