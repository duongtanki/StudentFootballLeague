using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.DataAccess.Data;

namespace StudentFootballLeague.BusinessLogic.Services
{
    public interface ITournamentService
    {
        Task<(bool IsSuccess, int StatusCode, string Message)> DeleteOrCancelTournamentAsync(int id);
    }

    public class TournamentService : ITournamentService
    {
        private readonly AppDbContext _context;

        public TournamentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(bool IsSuccess, int StatusCode, string Message)> DeleteOrCancelTournamentAsync(int id)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.Matches)
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.TournamentId == id);

            if (tournament == null)
            {
                return (false, 404, "Không tìm thấy giải đấu.");
            }

            if (tournament.Matches.Any(m => m.Status == "Completed" || m.Status == "InProgress"))
            {
                return (false, 400, "Không thể xóa hoặc hủy giải đấu đã có trận đấu đang diễn ra hoặc đã hoàn thành.");
            }

            // Nếu giải đã có đội đăng ký hoặc có trận lên lịch -> Xóa mềm (chuyển trạng thái Cancelled) để bảo toàn dữ liệu và tránh lỗi FK
            if (tournament.Registrations.Any() || tournament.Matches.Any())
            {
                if (tournament.Status == "Cancelled")
                {
                    return (false, 400, "Giải đấu này đã ở trạng thái hủy (Cancelled) trước đó.");
                }

                tournament.Status = "Cancelled";
                foreach (var match in tournament.Matches.Where(m => m.Status == "Scheduled" || m.Status == "Postponed"))
                {
                    match.Status = "Cancelled";
                }
                await _context.SaveChangesAsync();
                return (true, 200, "Giải đấu đã phát sinh hồ sơ đăng ký hoặc lịch thi đấu nên đã được chuyển sang trạng thái 'Cancelled' (Xóa mềm). Các trận chưa đá đã được tự động hủy.");
            }

            // Nếu giải hoàn toàn chưa có dữ liệu liên quan -> Xóa cứng an toàn
            _context.Tournaments.Remove(tournament);
            await _context.SaveChangesAsync();
            return (true, 200, "Đã xóa giải đấu thành công.");
        }
    }
}
