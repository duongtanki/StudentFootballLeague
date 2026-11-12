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
    public interface IKnockoutService
    {
        Task<(bool IsSuccess, string? ErrorMessage, KnockoutSetupReadDto? Data)> GetKnockoutSetupAsync(int tournamentId);
        Task<(bool IsSuccess, string? ErrorMessage, KnockoutBracketReadDto? Bracket)> GenerateBracketAsync(int tournamentId, GenerateKnockoutBracketDto dto);
        Task<KnockoutBracketReadDto?> GetBracketAsync(int tournamentId);
        Task<(bool IsSuccess, string? ErrorMessage)> ResetBracketAsync(int tournamentId);
        Task<(bool IsSuccess, string? ErrorMessage)> AdvanceWinnerOrSyncAsync(Match match);
    }

    public class KnockoutService : IKnockoutService
    {
        private readonly AppDbContext _context;
        private readonly IStandingsService _standingsService;

        public KnockoutService(AppDbContext context, IStandingsService standingsService)
        {
            _context = context;
            _standingsService = standingsService;
        }

        public async Task<(bool IsSuccess, string? ErrorMessage, KnockoutSetupReadDto? Data)> GetKnockoutSetupAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TournamentId == tournamentId);

            if (tournament == null)
            {
                return (false, "Không tìm thấy giải đấu.", null);
            }

            var groups = await _standingsService.GetGroupsAsync(tournamentId);
            var groupBlocks = new List<GroupStandingsBlockDto>();
            var approvedTeams = new List<QualifiedTeamOptionDto>();

            foreach (var g in groups)
            {
                var standings = await _standingsService.GetStandingsAsync(tournamentId, g);
                groupBlocks.Add(new GroupStandingsBlockDto
                {
                    GroupName = g,
                    Standings = standings
                });

                foreach (var s in standings)
                {
                    approvedTeams.Add(new QualifiedTeamOptionDto
                    {
                        TeamId = s.TeamId,
                        TeamName = s.TeamName,
                        GroupName = s.GroupName,
                        RankInGroup = s.Rank,
                        Points = s.Points,
                        GoalDifference = s.GoalDifference,
                        GoalsFor = s.GoalsFor,
                        FairPlayPoints = s.FairPlayPoints
                    });
                }
            }

            // Bao gồm cả đội đã duyệt nhưng chưa phân bảng (nếu có)
            var ungroupedApproved = await _context.TeamRegistrations
                .Include(r => r.Team)
                .Where(r => r.TournamentId == tournamentId && r.ApprovalStatus == "Approved" && r.GroupName == null)
                .ToListAsync();

            foreach (var reg in ungroupedApproved)
            {
                approvedTeams.Add(new QualifiedTeamOptionDto
                {
                    TeamId = reg.TeamId,
                    TeamName = reg.Team?.TeamName ?? "",
                    GroupName = null,
                    RankInGroup = 0,
                    Points = reg.Points,
                    GoalDifference = reg.GoalsFor - reg.GoalsAgainst,
                    GoalsFor = reg.GoalsFor,
                    FairPlayPoints = reg.FairPlayPoints
                });
            }

            var groupMatches = await _context.Matches
                .AsNoTracking()
                .Where(m => m.TournamentId == tournamentId && m.Stage == "Group" && m.Status != "Cancelled")
                .ToListAsync();

            var knockoutMatches = await _context.Matches
                .AsNoTracking()
                .Where(m => m.TournamentId == tournamentId && m.Stage == "Knockout" && m.Status != "Cancelled")
                .ToListAsync();

            int totalGroupMatches = groupMatches.Count;
            int completedGroupMatches = groupMatches.Count(m => m.Status == "Completed");
            int remainingGroupMatches = totalGroupMatches - completedGroupMatches;
            bool hasExistingBracket = knockoutMatches.Any();
            bool canReset = hasExistingBracket && !knockoutMatches.Any(m => m.Status == "InProgress" || m.Status == "Completed");

            var suggestedPairs = BuildSuggestedPairs(tournament, groupBlocks, approvedTeams);

            var setup = new KnockoutSetupReadDto
            {
                TournamentId = tournament.TournamentId,
                TournamentName = tournament.Name,
                TournamentStatus = tournament.Status,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                GroupCount = groups.Count > 0 ? groups.Count : tournament.GroupCount,
                KnockoutTeamCount = tournament.KnockoutTeamCount > 0 ? tournament.KnockoutTeamCount : 8,
                HasThirdPlaceMatch = tournament.HasThirdPlaceMatch,
                TotalGroupMatchesCount = totalGroupMatches,
                CompletedGroupMatchesCount = completedGroupMatches,
                RemainingGroupMatchesCount = remainingGroupMatches,
                HasExistingBracket = hasExistingBracket,
                CanResetBracket = canReset,
                GroupStandings = groupBlocks,
                ApprovedTeams = approvedTeams
                    .OrderBy(t => t.GroupName ?? "ZZZ")
                    .ThenBy(t => t.RankInGroup)
                    .ThenBy(t => t.TeamName)
                    .ToList(),
                SuggestedPairs = suggestedPairs
            };

            return (true, null, setup);
        }

        public async Task<(bool IsSuccess, string? ErrorMessage, KnockoutBracketReadDto? Bracket)> GenerateBracketAsync(int tournamentId, GenerateKnockoutBracketDto dto)
        {
            var tournament = await _context.Tournaments
                .FirstOrDefaultAsync(t => t.TournamentId == tournamentId);

            if (tournament == null)
            {
                return (false, "Không tìm thấy giải đấu.", null);
            }

            if (tournament.Status == "Cancelled")
            {
                return (false, "Không thể tạo vòng Knockout cho giải đấu đã bị hủy.", null);
            }

            var hasExistingKnockout = await _context.Matches
                .AnyAsync(m => m.TournamentId == tournamentId && m.Stage == "Knockout" && m.Status != "Cancelled");

            if (hasExistingKnockout)
            {
                return (false, "Giải đấu này đã có cây đấu Knockout. Vui lòng xóa/reset nhánh cũ trước khi thiết lập lại.", null);
            }

            if (dto.Pairs == null || (dto.Pairs.Count != 2 && dto.Pairs.Count != 4 && dto.Pairs.Count != 8))
            {
                return (false, "Số cặp đấu khởi đầu phải là 2 cặp (Bán Kết - 4 đội), 4 cặp (Tứ Kết - 8 đội) hoặc 8 cặp (Vòng 1/8 - 16 đội).", null);
            }

            var orderedPairs = dto.Pairs.OrderBy(p => p.BracketOrder).ToList();

            // Kiểm tra hợp lệ đội bóng: không trùng đội giữa tất cả các slot
            var approvedTeamIds = await _context.TeamRegistrations
                .Where(r => r.TournamentId == tournamentId && r.ApprovalStatus == "Approved")
                .Select(r => r.TeamId)
                .ToListAsync();

            var approvedSet = approvedTeamIds.ToHashSet();
            var usedTeams = new HashSet<int>();

            foreach (var pair in orderedPairs)
            {
                if (pair.HomeTeamId <= 0 || pair.AwayTeamId <= 0)
                {
                    return (false, $"Cặp đấu #{pair.BracketOrder} chưa chọn đủ cả Đội chủ nhà và Đội khách.", null);
                }

                if (pair.HomeTeamId == pair.AwayTeamId)
                {
                    return (false, $"Cặp đấu #{pair.BracketOrder} có Đội chủ nhà và Đội khách trùng nhau.", null);
                }

                if (!approvedSet.Contains(pair.HomeTeamId) || !approvedSet.Contains(pair.AwayTeamId))
                {
                    return (false, $"Cặp đấu #{pair.BracketOrder} chứa đội bóng chưa được duyệt tham gia giải đấu này.", null);
                }

                if (!usedTeams.Add(pair.HomeTeamId) || !usedTeams.Add(pair.AwayTeamId))
                {
                    return (false, "Một đội bóng không được xuất hiện ở nhiều hơn một cặp đấu trong cùng vòng Knockout.", null);
                }
            }

            // Kiểm tra xung đột lịch thi đấu ±90 phút cho các cặp đấu đầu tiên
            var existingActiveMatches = await _context.Matches
                .AsNoTracking()
                .Where(m => m.Status != "Cancelled")
                .ToListAsync();

            for (int i = 0; i < orderedPairs.Count; i++)
            {
                var p = orderedPairs[i];
                var winStart = p.MatchDateTime.AddMinutes(-90);
                var winEnd = p.MatchDateTime.AddMinutes(90);

                // Check với trận đã có trong DB
                foreach (var ex in existingActiveMatches)
                {
                    if (ex.MatchDateTime >= winStart && ex.MatchDateTime <= winEnd)
                    {
                        if (string.Equals(ex.PitchName, p.PitchName, StringComparison.OrdinalIgnoreCase))
                        {
                            return (false, $"Cặp #{p.BracketOrder}: Sân '{p.PitchName}' bị trùng lịch với trận #{ex.MatchId} lúc {ex.MatchDateTime:HH:mm dd/MM/yyyy} (±90 phút).", null);
                        }

                        if (ex.HomeTeamId == p.HomeTeamId || ex.AwayTeamId == p.HomeTeamId ||
                            ex.HomeTeamId == p.AwayTeamId || ex.AwayTeamId == p.AwayTeamId)
                        {
                            return (false, $"Cặp #{p.BracketOrder}: Có đội bóng bị trùng lịch thi đấu lúc {ex.MatchDateTime:HH:mm dd/MM/yyyy} (±90 phút).", null);
                        }
                    }
                }

                // Check giữa các cặp mới với nhau
                for (int j = i + 1; j < orderedPairs.Count; j++)
                {
                    var other = orderedPairs[j];
                    if (other.MatchDateTime >= winStart && other.MatchDateTime <= winEnd &&
                        string.Equals(other.PitchName, p.PitchName, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"Cặp #{p.BracketOrder} và Cặp #{other.BracketOrder} bị trùng sân '{p.PitchName}' trong khoảng ±90 phút.", null);
                    }
                }
            }

            DateTime GetDefaultRoundDate(int round, DateTime fallback)
            {
                var cfg = dto.SubsequentRounds?.FirstOrDefault(r => r.KnockoutRound == round);
                return cfg != null && cfg.DefaultDateTime != default ? cfg.DefaultDateTime : fallback;
            }

            string GetDefaultRoundPitch(int round, string fallback)
            {
                var cfg = dto.SubsequentRounds?.FirstOrDefault(r => r.KnockoutRound == round);
                return cfg != null && !string.IsNullOrWhiteSpace(cfg.DefaultPitchName) ? cfg.DefaultPitchName : fallback;
            }

            var maxInitialDate = orderedPairs.Max(p => p.MatchDateTime);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tạo trận Chung Kết (KnockoutRound = 2)
                var finalDate = GetDefaultRoundDate(2, maxInitialDate.AddDays(orderedPairs.Count >= 4 ? 6 : 3).Date.AddHours(18));
                var finalPitch = GetDefaultRoundPitch(2, "Sân Vận Động Trung Tâm");

                var finalMatch = new Match
                {
                    TournamentId = tournamentId,
                    GroupName = null,
                    RoundName = "Chung Kết",
                    Stage = "Knockout",
                    KnockoutRound = 2,
                    BranchName = "Chung Kết",
                    BracketOrder = 1,
                    HomeSourceLabel = "Thắng Bán Kết 1",
                    AwaySourceLabel = "Thắng Bán Kết 2",
                    HomeTeamId = null,
                    AwayTeamId = null,
                    MatchDateTime = finalDate,
                    PitchName = finalPitch,
                    Status = "Scheduled"
                };
                _context.Matches.Add(finalMatch);

                // 2. Tạo trận Tranh Hạng 3 (KnockoutRound = 3) nếu có
                Match? thirdPlaceMatch = null;
                if (dto.HasThirdPlaceMatch)
                {
                    var thirdDate = GetDefaultRoundDate(3, finalDate.AddHours(-2));
                    var thirdPitch = GetDefaultRoundPitch(3, finalPitch);

                    thirdPlaceMatch = new Match
                    {
                        TournamentId = tournamentId,
                        GroupName = null,
                        RoundName = "Tranh Hạng 3",
                        Stage = "Knockout",
                        KnockoutRound = 3,
                        BranchName = "Tranh Hạng 3",
                        BracketOrder = 1,
                        HomeSourceLabel = "Thua Bán Kết 1",
                        AwaySourceLabel = "Thua Bán Kết 2",
                        HomeTeamId = null,
                        AwayTeamId = null,
                        MatchDateTime = thirdDate,
                        PitchName = thirdPitch,
                        Status = "Scheduled"
                    };
                    _context.Matches.Add(thirdPlaceMatch);
                }

                await _context.SaveChangesAsync();

                // 3. Tạo Bán Kết (KnockoutRound = 4)
                if (orderedPairs.Count == 2)
                {
                    // Bắt đầu ngay từ Bán Kết (4 đội)
                    for (int i = 0; i < 2; i++)
                    {
                        var p = orderedPairs[i];
                        var sf = new Match
                        {
                            TournamentId = tournamentId,
                            GroupName = null,
                            RoundName = "Bán Kết",
                            Stage = "Knockout",
                            KnockoutRound = 4,
                            BranchName = string.IsNullOrWhiteSpace(p.BranchName) ? $"Nhánh {i + 1}" : p.BranchName,
                            BracketOrder = i + 1,
                            HomeSourceLabel = p.HomeSourceLabel,
                            AwaySourceLabel = p.AwaySourceLabel,
                            HomeTeamId = p.HomeTeamId,
                            AwayTeamId = p.AwayTeamId,
                            MatchDateTime = p.MatchDateTime,
                            PitchName = p.PitchName,
                            RefereeId = p.RefereeId,
                            NextMatchId = finalMatch.MatchId,
                            NextMatchSlot = i == 0 ? "Home" : "Away",
                            LoserNextMatchId = thirdPlaceMatch?.MatchId,
                            LoserNextMatchSlot = thirdPlaceMatch != null ? (i == 0 ? "Home" : "Away") : null,
                            Status = "Scheduled"
                        };
                        _context.Matches.Add(sf);
                    }
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Có Tứ Kết (hoặc Vòng 1/8): tạo 2 trận Bán Kết chờ sẵn
                    var sfBaseDate = GetDefaultRoundDate(4, maxInitialDate.AddDays(orderedPairs.Count == 8 ? 6 : 3).Date.AddHours(16));
                    var sfPitch = GetDefaultRoundPitch(4, "Sân Vận Động Trung Tâm");

                    var sf1 = new Match
                    {
                        TournamentId = tournamentId,
                        GroupName = null,
                        RoundName = "Bán Kết",
                        Stage = "Knockout",
                        KnockoutRound = 4,
                        BranchName = "Nhánh 1",
                        BracketOrder = 1,
                        HomeSourceLabel = "Thắng Tứ Kết 1",
                        AwaySourceLabel = "Thắng Tứ Kết 2",
                        HomeTeamId = null,
                        AwayTeamId = null,
                        MatchDateTime = sfBaseDate,
                        PitchName = sfPitch,
                        NextMatchId = finalMatch.MatchId,
                        NextMatchSlot = "Home",
                        LoserNextMatchId = thirdPlaceMatch?.MatchId,
                        LoserNextMatchSlot = thirdPlaceMatch != null ? "Home" : null,
                        Status = "Scheduled"
                    };

                    var sf2 = new Match
                    {
                        TournamentId = tournamentId,
                        GroupName = null,
                        RoundName = "Bán Kết",
                        Stage = "Knockout",
                        KnockoutRound = 4,
                        BranchName = "Nhánh 2",
                        BracketOrder = 2,
                        HomeSourceLabel = "Thắng Tứ Kết 3",
                        AwaySourceLabel = "Thắng Tứ Kết 4",
                        HomeTeamId = null,
                        AwayTeamId = null,
                        MatchDateTime = sfBaseDate.AddHours(2),
                        PitchName = sfPitch,
                        NextMatchId = finalMatch.MatchId,
                        NextMatchSlot = "Away",
                        LoserNextMatchId = thirdPlaceMatch?.MatchId,
                        LoserNextMatchSlot = thirdPlaceMatch != null ? "Away" : null,
                        Status = "Scheduled"
                    };

                    _context.Matches.AddRange(sf1, sf2);
                    await _context.SaveChangesAsync();

                    if (orderedPairs.Count == 4)
                    {
                        // Bắt đầu từ Tứ Kết (8 đội chia 2 nhánh)
                        for (int i = 0; i < 4; i++)
                        {
                            var p = orderedPairs[i];
                            bool isBranch1 = i < 2;
                            var targetSf = isBranch1 ? sf1 : sf2;
                            var slot = (i % 2 == 0) ? "Home" : "Away";

                            var qf = new Match
                            {
                                TournamentId = tournamentId,
                                GroupName = null,
                                RoundName = "Tứ Kết",
                                Stage = "Knockout",
                                KnockoutRound = 8,
                                BranchName = isBranch1 ? "Nhánh 1" : "Nhánh 2",
                                BracketOrder = i + 1,
                                HomeSourceLabel = p.HomeSourceLabel,
                                AwaySourceLabel = p.AwaySourceLabel,
                                HomeTeamId = p.HomeTeamId,
                                AwayTeamId = p.AwayTeamId,
                                MatchDateTime = p.MatchDateTime,
                                PitchName = p.PitchName,
                                RefereeId = p.RefereeId,
                                NextMatchId = targetSf.MatchId,
                                NextMatchSlot = slot,
                                Status = "Scheduled"
                            };
                            _context.Matches.Add(qf);
                        }
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Bắt đầu từ Vòng 1/8 (16 đội -> 4 Tứ kết -> 2 Bán kết -> Chung kết)
                        var qfBaseDate = GetDefaultRoundDate(8, maxInitialDate.AddDays(3).Date.AddHours(15));
                        var qfPitch = GetDefaultRoundPitch(8, "Sân cỏ nhân tạo số 1 - Khu Thể chất");
                        var qfMatches = new List<Match>();

                        for (int i = 0; i < 4; i++)
                        {
                            bool isBranch1 = i < 2;
                            var targetSf = isBranch1 ? sf1 : sf2;
                            var qf = new Match
                            {
                                TournamentId = tournamentId,
                                GroupName = null,
                                RoundName = "Tứ Kết",
                                Stage = "Knockout",
                                KnockoutRound = 8,
                                BranchName = isBranch1 ? "Nhánh 1" : "Nhánh 2",
                                BracketOrder = i + 1,
                                HomeSourceLabel = $"Thắng Vòng 1/8 #{i * 2 + 1}",
                                AwaySourceLabel = $"Thắng Vòng 1/8 #{i * 2 + 2}",
                                HomeTeamId = null,
                                AwayTeamId = null,
                                MatchDateTime = qfBaseDate.AddHours(i * 2),
                                PitchName = qfPitch,
                                NextMatchId = targetSf.MatchId,
                                NextMatchSlot = (i % 2 == 0) ? "Home" : "Away",
                                Status = "Scheduled"
                            };
                            qfMatches.Add(qf);
                            _context.Matches.Add(qf);
                        }
                        await _context.SaveChangesAsync();

                        for (int i = 0; i < 8; i++)
                        {
                            var p = orderedPairs[i];
                            bool isBranch1 = i < 4;
                            var targetQf = qfMatches[i / 2];

                            var r16 = new Match
                            {
                                TournamentId = tournamentId,
                                GroupName = null,
                                RoundName = "Vòng 1/8",
                                Stage = "Knockout",
                                KnockoutRound = 16,
                                BranchName = isBranch1 ? "Nhánh 1" : "Nhánh 2",
                                BracketOrder = i + 1,
                                HomeSourceLabel = p.HomeSourceLabel,
                                AwaySourceLabel = p.AwaySourceLabel,
                                HomeTeamId = p.HomeTeamId,
                                AwayTeamId = p.AwayTeamId,
                                MatchDateTime = p.MatchDateTime,
                                PitchName = p.PitchName,
                                RefereeId = p.RefereeId,
                                NextMatchId = targetQf.MatchId,
                                NextMatchSlot = (i % 2 == 0) ? "Home" : "Away",
                                Status = "Scheduled"
                            };
                            _context.Matches.Add(r16);
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                // Cập nhật cấu hình giải đấu khớp với cây đấu vừa sinh
                tournament.KnockoutTeamCount = orderedPairs.Count * 2;
                tournament.HasThirdPlaceMatch = dto.HasThirdPlaceMatch;
                tournament.ChampionTeamId = null;
                if (tournament.Status == "Registering")
                {
                    tournament.Status = "Ongoing";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi khi sinh cây đấu Knockout: {ex.Message}", null);
            }

            var bracket = await GetBracketAsync(tournamentId);
            return (true, null, bracket);
        }

        public async Task<KnockoutBracketReadDto?> GetBracketAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments
                .Include(t => t.ChampionTeam)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TournamentId == tournamentId);

            if (tournament == null)
            {
                return null;
            }

            var knockoutMatches = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.WinnerTeam)
                .Include(m => m.Referee)
                .Include(m => m.Events)
                    .ThenInclude(e => e.Player)
                .Where(m => m.TournamentId == tournamentId && m.Stage == "Knockout" && m.Status != "Cancelled")
                .OrderByDescending(m => m.KnockoutRound)
                .ThenBy(m => m.BracketOrder)
                .ThenBy(m => m.MatchDateTime)
                .AsNoTracking()
                .ToListAsync();

            bool canReset = knockoutMatches.Any() && !knockoutMatches.Any(m => m.Status == "InProgress" || m.Status == "Completed");

            var rounds = knockoutMatches
                .GroupBy(m => new
                {
                    Round = m.KnockoutRound ?? InferRoundOrder(m.RoundName),
                    Name = m.RoundName
                })
                .OrderByDescending(g => g.Key.Round)
                .Select(g => new KnockoutRoundReadDto
                {
                    KnockoutRound = g.Key.Round,
                    RoundName = g.Key.Name,
                    Matches = g.OrderBy(m => m.BracketOrder ?? 99).ThenBy(m => m.MatchDateTime).Select(m => new MatchReadDto
                    {
                        MatchId = m.MatchId,
                        TournamentId = m.TournamentId,
                        TournamentName = tournament.Name,
                        GroupName = m.GroupName,
                        RoundName = m.RoundName,
                        Stage = m.Stage,
                        KnockoutRound = m.KnockoutRound,
                        BranchName = m.BranchName,
                        BracketOrder = m.BracketOrder,
                        HomeSourceLabel = m.HomeSourceLabel,
                        AwaySourceLabel = m.AwaySourceLabel,
                        HomeTeamId = m.HomeTeamId,
                        HomeTeamName = m.HomeTeam?.TeamName ?? m.HomeSourceLabel ?? "Chờ xác định",
                        AwayTeamId = m.AwayTeamId,
                        AwayTeamName = m.AwayTeam?.TeamName ?? m.AwaySourceLabel ?? "Chờ xác định",
                        WinnerTeamId = m.WinnerTeamId,
                        WinnerTeamName = m.WinnerTeam?.TeamName,
                        NextMatchId = m.NextMatchId,
                        NextMatchSlot = m.NextMatchSlot,
                        LoserNextMatchId = m.LoserNextMatchId,
                        LoserNextMatchSlot = m.LoserNextMatchSlot,
                        MatchDateTime = m.MatchDateTime,
                        PitchName = m.PitchName,
                        RefereeId = m.RefereeId,
                        RefereeName = m.Referee?.FullName,
                        HomeScore = m.HomeScore,
                        AwayScore = m.AwayScore,
                        HomePenalty = m.HomePenalty,
                        AwayPenalty = m.AwayPenalty,
                        Status = m.Status,
                        Events = m.Events.Select(e => new MatchEventReadDto
                        {
                            EventId = e.EventId,
                            MatchId = e.MatchId,
                            TeamId = e.TeamId,
                            TeamName = e.TeamId == m.HomeTeamId ? (m.HomeTeam?.TeamName ?? "") : (m.AwayTeam?.TeamName ?? ""),
                            PlayerId = e.PlayerId,
                            PlayerName = e.Player?.FullName ?? "",
                            JerseyNumber = e.Player?.JerseyNumber ?? 0,
                            EventType = e.EventType,
                            Minute = e.Minute,
                            Description = e.Description
                        }).OrderBy(e => e.Minute).ToList()
                    }).ToList()
                })
                .ToList();

            return new KnockoutBracketReadDto
            {
                TournamentId = tournament.TournamentId,
                TournamentName = tournament.Name,
                TournamentStatus = tournament.Status,
                KnockoutTeamCount = tournament.KnockoutTeamCount,
                HasThirdPlaceMatch = tournament.HasThirdPlaceMatch,
                ChampionTeamId = tournament.ChampionTeamId,
                ChampionTeamName = tournament.ChampionTeam?.TeamName,
                CanResetBracket = canReset,
                Rounds = rounds
            };
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> ResetBracketAsync(int tournamentId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null)
            {
                return (false, "Không tìm thấy giải đấu.");
            }

            var knockoutMatches = await _context.Matches
                .Where(m => m.TournamentId == tournamentId && m.Stage == "Knockout")
                .ToListAsync();

            if (!knockoutMatches.Any())
            {
                return (false, "Giải đấu này chưa có nhánh Knockout để xóa.");
            }

            if (knockoutMatches.Any(m => m.Status == "InProgress" || m.Status == "Completed"))
            {
                return (false, "Không thể xóa nhánh Knockout vì đã có trận đang diễn ra hoặc đã hoàn thành.");
            }

            // Gỡ self-FK trước khi xóa để tránh xung đột FK
            foreach (var m in knockoutMatches)
            {
                m.NextMatchId = null;
                m.LoserNextMatchId = null;
            }
            await _context.SaveChangesAsync();

            _context.Matches.RemoveRange(knockoutMatches);
            tournament.ChampionTeamId = null;
            if (tournament.Status == "Completed")
            {
                tournament.Status = "Ongoing";
            }

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> AdvanceWinnerOrSyncAsync(Match match)
        {
            if (match.Stage != "Knockout")
            {
                return (true, null);
            }

            var tournament = await _context.Tournaments.FindAsync(match.TournamentId);

            // Nếu trận bị đưa về trạng thái chưa hoàn thành -> gỡ slot ở vòng sau nếu vòng sau chưa đá
            if (match.Status != "Completed")
            {
                if (match.WinnerTeamId.HasValue)
                {
                    if (match.NextMatchId.HasValue)
                    {
                        var nextMatch = await _context.Matches.FindAsync(match.NextMatchId.Value);
                        if (nextMatch != null)
                        {
                            if (nextMatch.Status == "InProgress" || nextMatch.Status == "Completed")
                            {
                                return (false, "Không thể mở lại kết quả trận đấu này vì trận vòng tiếp theo đã bắt đầu hoặc hoàn tất.");
                            }
                            ClearMatchSlot(nextMatch, match.NextMatchSlot);
                        }
                    }

                    if (match.LoserNextMatchId.HasValue)
                    {
                        var loserNextMatch = await _context.Matches.FindAsync(match.LoserNextMatchId.Value);
                        if (loserNextMatch != null)
                        {
                            if (loserNextMatch.Status == "InProgress" || loserNextMatch.Status == "Completed")
                            {
                                return (false, "Không thể mở lại kết quả trận đấu này vì trận Tranh Hạng 3 đã bắt đầu hoặc hoàn tất.");
                            }
                            ClearMatchSlot(loserNextMatch, match.LoserNextMatchSlot);
                        }
                    }

                    match.WinnerTeamId = null;
                    if (match.KnockoutRound == 2 && tournament != null)
                    {
                        tournament.ChampionTeamId = null;
                        if (tournament.Status == "Completed")
                        {
                            tournament.Status = "Ongoing";
                        }
                    }
                }

                return (true, null);
            }

            // Trận Knockout đã Completed -> xác định đội thắng & đội thua
            if (!match.HomeTeamId.HasValue || !match.AwayTeamId.HasValue)
            {
                return (false, "Trận Knockout chưa xác định đủ 2 đội thi đấu.");
            }

            if (!match.HomeScore.HasValue || !match.AwayScore.HasValue)
            {
                return (false, "Vui lòng nhập đầy đủ tỷ số trận đấu.");
            }

            int winnerTeamId;
            int loserTeamId;

            if (match.HomeScore.Value > match.AwayScore.Value)
            {
                winnerTeamId = match.HomeTeamId.Value;
                loserTeamId = match.AwayTeamId.Value;
            }
            else if (match.AwayScore.Value > match.HomeScore.Value)
            {
                winnerTeamId = match.AwayTeamId.Value;
                loserTeamId = match.HomeTeamId.Value;
            }
            else
            {
                // Hòa trong thời gian chính thức -> bắt buộc phân định bằng luân lưu
                if (!match.HomePenalty.HasValue || !match.AwayPenalty.HasValue || match.HomePenalty.Value == match.AwayPenalty.Value)
                {
                    return (false, "Trận vòng Knockout có tỷ số chính thức hòa bắt buộc phải có tỷ số luân lưu (Penalty) phân định thắng thua.");
                }

                if (match.HomePenalty.Value > match.AwayPenalty.Value)
                {
                    winnerTeamId = match.HomeTeamId.Value;
                    loserTeamId = match.AwayTeamId.Value;
                }
                else
                {
                    winnerTeamId = match.AwayTeamId.Value;
                    loserTeamId = match.HomeTeamId.Value;
                }
            }

            // Kiểm tra và đẩy đội thắng vào NextMatch
            if (match.NextMatchId.HasValue)
            {
                var nextMatch = await _context.Matches.FindAsync(match.NextMatchId.Value);
                if (nextMatch != null)
                {
                    var currentSlotTeam = GetMatchSlotTeamId(nextMatch, match.NextMatchSlot);
                    if ((nextMatch.Status == "InProgress" || nextMatch.Status == "Completed") && currentSlotTeam != winnerTeamId)
                    {
                        return (false, "Không thể thay đổi đội thắng vì trận vòng tiếp theo đã bắt đầu hoặc đã kết thúc.");
                    }

                    SetMatchSlot(nextMatch, match.NextMatchSlot, winnerTeamId);
                }
            }

            // Kiểm tra và đẩy đội thua Bán Kết vào Tranh Hạng 3
            if (match.LoserNextMatchId.HasValue)
            {
                var thirdPlaceMatch = await _context.Matches.FindAsync(match.LoserNextMatchId.Value);
                if (thirdPlaceMatch != null)
                {
                    var currentSlotTeam = GetMatchSlotTeamId(thirdPlaceMatch, match.LoserNextMatchSlot);
                    if ((thirdPlaceMatch.Status == "InProgress" || thirdPlaceMatch.Status == "Completed") && currentSlotTeam != loserTeamId)
                    {
                        return (false, "Không thể thay đổi kết quả vì trận Tranh Hạng 3 đã bắt đầu hoặc đã kết thúc.");
                    }

                    SetMatchSlot(thirdPlaceMatch, match.LoserNextMatchSlot, loserTeamId);
                }
            }

            match.WinnerTeamId = winnerTeamId;

            // Nếu là trận Chung Kết -> chốt nhà vô địch
            if ((match.KnockoutRound == 2 || string.Equals(match.RoundName, "Chung Kết", StringComparison.OrdinalIgnoreCase)) && tournament != null)
            {
                tournament.ChampionTeamId = winnerTeamId;
                tournament.Status = "Completed";
            }

            return (true, null);
        }

        private static int? GetMatchSlotTeamId(Match targetMatch, string? slot)
        {
            return string.Equals(slot, "Home", StringComparison.OrdinalIgnoreCase)
                ? targetMatch.HomeTeamId
                : targetMatch.AwayTeamId;
        }

        private static void SetMatchSlot(Match targetMatch, string? slot, int teamId)
        {
            if (string.Equals(slot, "Home", StringComparison.OrdinalIgnoreCase))
            {
                targetMatch.HomeTeamId = teamId;
            }
            else if (string.Equals(slot, "Away", StringComparison.OrdinalIgnoreCase))
            {
                targetMatch.AwayTeamId = teamId;
            }
        }

        private static void ClearMatchSlot(Match targetMatch, string? slot)
        {
            if (string.Equals(slot, "Home", StringComparison.OrdinalIgnoreCase))
            {
                targetMatch.HomeTeamId = null;
            }
            else if (string.Equals(slot, "Away", StringComparison.OrdinalIgnoreCase))
            {
                targetMatch.AwayTeamId = null;
            }
        }

        private static int InferRoundOrder(string roundName)
        {
            if (roundName.Contains("1/8", StringComparison.OrdinalIgnoreCase)) return 16;
            if (roundName.Contains("Tứ", StringComparison.OrdinalIgnoreCase)) return 8;
            if (roundName.Contains("Bán", StringComparison.OrdinalIgnoreCase)) return 4;
            if (roundName.Contains("Hạng 3", StringComparison.OrdinalIgnoreCase)) return 3;
            return 2;
        }

        private static List<KnockoutPairInputDto> BuildSuggestedPairs(
            Tournament tournament,
            List<GroupStandingsBlockDto> groupBlocks,
            List<QualifiedTeamOptionDto> approvedTeams)
        {
            var result = new List<KnockoutPairInputDto>();
            int targetTeamCount = tournament.KnockoutTeamCount > 0 ? tournament.KnockoutTeamCount : 8;
            if (approvedTeams.Count < targetTeamCount)
            {
                targetTeamCount = approvedTeams.Count >= 8 ? 8 : (approvedTeams.Count >= 4 ? 4 : 0);
            }

            if (targetTeamCount < 4) return result;

            var baseDate = tournament.EndDate.Date > DateTime.Today
                ? tournament.EndDate.Date.AddDays(-6).AddHours(15).AddMinutes(30)
                : DateTime.Today.AddDays(1).AddHours(15).AddMinutes(30);

            int pairCount = targetTeamCount / 2;

            // Chọn top teams theo thứ hạng trong bảng
            var orderedTeams = new List<QualifiedTeamOptionDto>();
            if (groupBlocks.Count >= 2)
            {
                int perGroup = Math.Max(1, targetTeamCount / groupBlocks.Count);
                for (int r = 1; r <= perGroup; r++)
                {
                    foreach (var gb in groupBlocks)
                    {
                        var team = gb.Standings.FirstOrDefault(s => s.Rank == r);
                        if (team != null)
                        {
                            orderedTeams.Add(approvedTeams.First(t => t.TeamId == team.TeamId));
                        }
                    }
                }

                // Nếu còn thiếu (VD 3 bảng lấy 8 đội -> thêm hạng 3 xuất sắc nhất)
                if (orderedTeams.Count < targetTeamCount)
                {
                    var remaining = approvedTeams
                        .Where(t => orderedTeams.All(o => o.TeamId != t.TeamId))
                        .OrderBy(t => t.RankInGroup)
                        .ThenByDescending(t => t.Points)
                        .ThenByDescending(t => t.GoalDifference)
                        .ThenByDescending(t => t.GoalsFor)
                        .Take(targetTeamCount - orderedTeams.Count);
                    orderedTeams.AddRange(remaining);
                }
            }
            else
            {
                orderedTeams.AddRange(approvedTeams.Take(targetTeamCount));
            }

            if (orderedTeams.Count < targetTeamCount) return result;

            // Xếp cặp cân bằng: đội hạt giống cao gặp đội thấp hơn theo 2 nhánh đối xứng
            for (int i = 0; i < pairCount; i++)
            {
                var home = orderedTeams[i];
                var away = orderedTeams[targetTeamCount - 1 - i];
                bool isBranch1 = i < pairCount / 2;

                result.Add(new KnockoutPairInputDto
                {
                    BracketOrder = i + 1,
                    BranchName = isBranch1 ? "Nhánh 1" : "Nhánh 2",
                    HomeTeamId = home.TeamId,
                    AwayTeamId = away.TeamId,
                    HomeSourceLabel = !string.IsNullOrEmpty(home.GroupName) ? $"Hạng {home.RankInGroup} {home.GroupName}" : null,
                    AwaySourceLabel = !string.IsNullOrEmpty(away.GroupName) ? $"Hạng {away.RankInGroup} {away.GroupName}" : null,
                    MatchDateTime = baseDate.AddHours(i * 2),
                    PitchName = i % 2 == 0 ? "Sân cỏ nhân tạo số 1 - Khu Thể chất" : "Sân cỏ nhân tạo số 2 - Khu Thể chất"
                });
            }

            return result;
        }
    }
}
