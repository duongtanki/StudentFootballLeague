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
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateMatchScheduleAsync(int matchId, MatchScheduleUpdateDto dto);
        Task<(bool IsSuccess, string? ErrorMessage)> UpdateMatchResultAsync(int matchId, MatchResultUpdateDto dto);
    }

    public class MatchService : IMatchService
    {
        private readonly AppDbContext _context;
        private readonly IStandingsService _standingsService;
        private readonly IKnockoutService _knockoutService;

        public MatchService(AppDbContext context, IStandingsService standingsService, IKnockoutService knockoutService)
        {
            _context = context;
            _standingsService = standingsService;
            _knockoutService = knockoutService;
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

            if (tournament.Status == "Cancelled" || tournament.Status == "Completed")
            {
                return (false, "Không thể lên lịch thi đấu cho giải đấu đã bị hủy hoặc đã kết thúc.", null);
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

            var isGroupMatch = !string.IsNullOrWhiteSpace(dto.GroupName);
            var match = new Match
            {
                TournamentId = dto.TournamentId,
                GroupName = isGroupMatch ? dto.GroupName : null,
                RoundName = dto.RoundName,
                Stage = isGroupMatch ? "Group" : "Knockout",
                KnockoutRound = isGroupMatch ? null : InferKnockoutRound(dto.RoundName),
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

        public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateMatchScheduleAsync(int matchId, MatchScheduleUpdateDto dto)
        {
            var match = await _context.Matches.FindAsync(matchId);
            if (match == null)
            {
                return (false, "Trận đấu không tồn tại.");
            }

            if (match.Status == "Completed")
            {
                return (false, "Không thể đổi lịch trận đấu đã hoàn thành.");
            }

            var windowStart = dto.MatchDateTime.AddMinutes(-90);
            var windowEnd = dto.MatchDateTime.AddMinutes(90);

            var conflicts = await _context.Matches
                .Where(m => m.MatchId != matchId && m.Status != "Cancelled" && m.MatchDateTime >= windowStart && m.MatchDateTime <= windowEnd)
                .ToListAsync();

            foreach (var existing in conflicts)
            {
                if (string.Equals(existing.PitchName, dto.PitchName, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Sân '{dto.PitchName}' đã có trận đấu diễn ra vào lúc {existing.MatchDateTime:HH:mm dd/MM/yyyy} (cần cách nhau ít nhất 90 phút).");
                }

                if (match.HomeTeamId.HasValue && (existing.HomeTeamId == match.HomeTeamId || existing.AwayTeamId == match.HomeTeamId))
                {
                    return (false, $"Đội nhà đang có lịch thi đấu lúc {existing.MatchDateTime:HH:mm dd/MM/yyyy} (cần nghỉ tối thiểu 90 phút).");
                }

                if (match.AwayTeamId.HasValue && (existing.HomeTeamId == match.AwayTeamId || existing.AwayTeamId == match.AwayTeamId))
                {
                    return (false, $"Đội khách đang có lịch thi đấu lúc {existing.MatchDateTime:HH:mm dd/MM/yyyy} (cần nghỉ tối thiểu 90 phút).");
                }
            }

            match.MatchDateTime = dto.MatchDateTime;
            match.PitchName = dto.PitchName;
            match.RefereeId = dto.RefereeId;

            await _context.SaveChangesAsync();
            return (true, null);
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

            if (!match.HomeTeamId.HasValue || !match.AwayTeamId.HasValue)
            {
                return (false, "Trận đấu vòng Knockout chưa xác định đủ 2 đội thi đấu, chưa thể cập nhật kết quả.");
            }

            bool isKnockout = string.Equals(match.Stage, "Knockout", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(match.GroupName);

            if (isKnockout && dto.Status == "Completed" && dto.HomeScore == dto.AwayScore)
            {
                if (!dto.HomePenalty.HasValue || !dto.AwayPenalty.HasValue || dto.HomePenalty.Value < 0 || dto.AwayPenalty.Value < 0 || dto.HomePenalty.Value == dto.AwayPenalty.Value)
                {
                    return (false, "Trận vòng Knockout có tỷ số chính thức hòa bắt buộc phải nhập tỷ số luân lưu (Penalty) phân định thắng thua.");
                }
            }

            match.HomeScore = dto.HomeScore;
            match.AwayScore = dto.AwayScore;
            match.HomePenalty = (isKnockout && dto.HomeScore == dto.AwayScore) ? dto.HomePenalty : null;
            match.AwayPenalty = (isKnockout && dto.HomeScore == dto.AwayScore) ? dto.AwayPenalty : null;
            match.Status = dto.Status;

            if (isKnockout)
            {
                var syncResult = await _knockoutService.AdvanceWinnerOrSyncAsync(match);
                if (!syncResult.IsSuccess)
                {
                    return (false, syncResult.ErrorMessage);
                }
            }

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

            // Chỉ tính toán lại Bảng xếp hạng đối với trận thuộc vòng bảng
            if (!isKnockout)
            {
                await _standingsService.RecalculateStandingsAsync(match.TournamentId, match.GroupName);
            }

            return (true, null);
        }

        private static int? InferKnockoutRound(string roundName)
        {
            if (roundName.Contains("1/8", StringComparison.OrdinalIgnoreCase)) return 16;
            if (roundName.Contains("Tứ", StringComparison.OrdinalIgnoreCase)) return 8;
            if (roundName.Contains("Bán", StringComparison.OrdinalIgnoreCase)) return 4;
            if (roundName.Contains("Hạng 3", StringComparison.OrdinalIgnoreCase)) return 3;
            if (roundName.Contains("Chung", StringComparison.OrdinalIgnoreCase)) return 2;
            return null;
        }
    }
}
