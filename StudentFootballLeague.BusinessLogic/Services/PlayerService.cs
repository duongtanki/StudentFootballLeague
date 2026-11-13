using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.BusinessLogic.Services
{
    public interface IPlayerService
    {
        Task<(bool IsSuccess, int StatusCode, string? ErrorMessage, Player? Player)> AddPlayerAsync(int teamId, int userId, string userRole, PlayerCreateDto dto);
        Task<(bool IsSuccess, int StatusCode, string? ErrorMessage, Player? Player)> UpdatePlayerAsync(int id, int userId, string userRole, PlayerUpdateDto dto);
        Task<(bool IsSuccess, int StatusCode, string? ErrorMessage)> SoftDeletePlayerAsync(int id, int userId, string userRole);
    }

    public class PlayerService : IPlayerService
    {
        private readonly AppDbContext _context;

        public PlayerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(bool IsSuccess, int StatusCode, string? ErrorMessage, Player? Player)> AddPlayerAsync(int teamId, int userId, string userRole, PlayerCreateDto dto)
        {
            var team = await _context.Teams.FindAsync(teamId);
            if (team == null)
            {
                return (false, 404, "Không tìm thấy đội bóng.", null);
            }

            if (userRole != "Admin" && team.ManagerId != userId)
            {
                return (false, 403, "Bạn không có quyền chỉnh sửa đội bóng này.", null);
            }

            // Chỉ kiểm tra trùng MSSV với các cầu thủ đang hoạt động (IsActive = true)
            var activeCodeExists = await _context.Players.AnyAsync(p => p.IsActive && p.StudentCode == dto.StudentCode);
            if (activeCodeExists)
            {
                return (false, 400, $"Cầu thủ có MSSV '{dto.StudentCode}' đã được đăng ký trong hệ thống.", null);
            }

            // Chỉ kiểm tra trùng số áo với các cầu thủ đang hoạt động trong cùng đội
            var activeNumberExists = await _context.Players.AnyAsync(p => p.IsActive && p.TeamId == teamId && p.JerseyNumber == dto.JerseyNumber);
            if (activeNumberExists)
            {
                return (false, 400, $"Số áo {dto.JerseyNumber} đã được cầu thủ khác trong đội sử dụng.", null);
            }

            var player = new Player
            {
                TeamId = teamId,
                StudentCode = dto.StudentCode,
                FullName = dto.FullName,
                DateOfBirth = dto.DateOfBirth,
                JerseyNumber = dto.JerseyNumber,
                Position = dto.Position,
                IsActive = true
            };

            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            return (true, 200, null, player);
        }

        public async Task<(bool IsSuccess, int StatusCode, string? ErrorMessage, Player? Player)> UpdatePlayerAsync(int id, int userId, string userRole, PlayerUpdateDto dto)
        {
            var player = await _context.Players
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.PlayerId == id && p.IsActive);

            if (player == null)
            {
                return (false, 404, "Không tìm thấy cầu thủ.", null);
            }

            if (userRole != "Admin" && player.Team != null && player.Team.ManagerId != userId)
            {
                return (false, 403, "Bạn không có quyền chỉnh sửa cầu thủ này.", null);
            }

            if (player.JerseyNumber != dto.JerseyNumber)
            {
                var existingNumber = await _context.Players
                    .AnyAsync(p => p.IsActive && p.TeamId == player.TeamId && p.JerseyNumber == dto.JerseyNumber && p.PlayerId != id);
                if (existingNumber)
                {
                    return (false, 400, $"Số áo {dto.JerseyNumber} đã được cầu thủ khác sử dụng.", null);
                }
            }

            player.FullName = dto.FullName;
            player.DateOfBirth = dto.DateOfBirth;
            player.JerseyNumber = dto.JerseyNumber;
            player.Position = dto.Position;
            player.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return (true, 200, null, player);
        }

        public async Task<(bool IsSuccess, int StatusCode, string? ErrorMessage)> SoftDeletePlayerAsync(int id, int userId, string userRole)
        {
            var player = await _context.Players
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.PlayerId == id);

            if (player == null || !player.IsActive)
            {
                return (false, 404, "Không tìm thấy cầu thủ hoặc cầu thủ đã bị xóa.");
            }

            if (userRole != "Admin" && player.Team != null && player.Team.ManagerId != userId)
            {
                return (false, 403, "Bạn không có quyền xóa cầu thủ này.");
            }

            // Nếu đội đang tham gia giải chưa kết thúc/chưa hủy (Approved), bảo đảm đội vẫn đủ quân số tối thiểu và còn ít nhất 1 GK
            var activeRegistrations = await _context.TeamRegistrations
                .Include(r => r.Tournament)
                .Where(r => r.TeamId == player.TeamId
                    && r.ApprovalStatus == "Approved"
                    && r.Tournament != null
                    && r.Tournament.Status != "Completed"
                    && r.Tournament.Status != "Cancelled")
                .ToListAsync();

            if (activeRegistrations.Any())
            {
                var remainingPlayers = await _context.Players
                    .Where(p => p.TeamId == player.TeamId && p.IsActive && p.PlayerId != id)
                    .ToListAsync();

                foreach (var reg in activeRegistrations)
                {
                    if (remainingPlayers.Count < reg.Tournament!.MinPlayersPerTeam)
                    {
                        return (false, 400, $"Không thể xóa cầu thủ vì đội đang tham gia giải '{reg.Tournament.Name}' yêu cầu tối thiểu {reg.Tournament.MinPlayersPerTeam} cầu thủ.");
                    }

                    if (!remainingPlayers.Any(p => p.Position == "GK"))
                    {
                        return (false, 400, $"Không thể xóa Thủ môn (GK) duy nhất khi đội đang tham gia giải '{reg.Tournament.Name}'.");
                    }
                }
            }

            // Xóa mềm để bảo toàn lịch sử sự kiện trận đấu (MatchEvents) và Vua phá lưới (V_PlayerStats)
            player.IsActive = false;
            await _context.SaveChangesAsync();

            return (true, 200, null);
        }
    }
}
