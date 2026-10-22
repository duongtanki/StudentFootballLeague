using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.DataAccess.Data;

namespace StudentFootballLeague.BusinessLogic.Services
{
    public interface IStandingsService
    {
        Task RecalculateStandingsAsync(int tournamentId, string? groupName = null);
        Task<List<StandingReadDto>> GetStandingsAsync(int tournamentId, string? groupName = null);
        Task<List<TopScorerDto>> GetTopScorersAsync(int tournamentId, int top = 10);
    }

    public class StandingsService : IStandingsService
    {
        private readonly AppDbContext _context;

        public StandingsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task RecalculateStandingsAsync(int tournamentId, string? groupName = null)
        {
            var registrationsQuery = _context.TeamRegistrations
                .Where(r => r.TournamentId == tournamentId && r.ApprovalStatus == "Approved");

            if (!string.IsNullOrEmpty(groupName))
            {
                registrationsQuery = registrationsQuery.Where(r => r.GroupName == groupName);
            }

            var registrations = await registrationsQuery.ToListAsync();

            // Reset stats
            foreach (var reg in registrations)
            {
                reg.MatchesPlayed = 0;
                reg.Wins = 0;
                reg.Draws = 0;
                reg.Losses = 0;
                reg.GoalsFor = 0;
                reg.GoalsAgainst = 0;
                reg.Points = 0;
                reg.FairPlayPoints = 0;
            }

            // Load all completed matches
            var matchesQuery = _context.Matches
                .Include(m => m.Events)
                .Where(m => m.TournamentId == tournamentId && m.Status == "Completed");

            if (!string.IsNullOrEmpty(groupName))
            {
                matchesQuery = matchesQuery.Where(m => m.GroupName == groupName);
            }

            var matches = await matchesQuery.ToListAsync();

            var regDict = registrations.ToDictionary(r => r.TeamId);

            foreach (var match in matches)
            {
                if (!match.HomeScore.HasValue || !match.AwayScore.HasValue) continue;

                if (regDict.TryGetValue(match.HomeTeamId, out var homeReg) &&
                    regDict.TryGetValue(match.AwayTeamId, out var awayReg))
                {
                    homeReg.MatchesPlayed++;
                    awayReg.MatchesPlayed++;

                    homeReg.GoalsFor += match.HomeScore.Value;
                    homeReg.GoalsAgainst += match.AwayScore.Value;

                    awayReg.GoalsFor += match.AwayScore.Value;
                    awayReg.GoalsAgainst += match.HomeScore.Value;

                    if (match.HomeScore > match.AwayScore)
                    {
                        homeReg.Wins++;
                        homeReg.Points += 3;
                        awayReg.Losses++;
                    }
                    else if (match.HomeScore < match.AwayScore)
                    {
                        awayReg.Wins++;
                        awayReg.Points += 3;
                        homeReg.Losses++;
                    }
                    else
                    {
                        homeReg.Draws++;
                        homeReg.Points += 1;
                        awayReg.Draws++;
                        awayReg.Points += 1;
                    }

                    // Calculate Fair-play points from cards
                    foreach (var evt in match.Events)
                    {
                        if (regDict.TryGetValue(evt.TeamId, out var cardReg))
                        {
                            if (evt.EventType == "YellowCard")
                            {
                                cardReg.FairPlayPoints -= 1;
                            }
                            else if (evt.EventType == "RedCard")
                            {
                                cardReg.FairPlayPoints -= 3;
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<StandingReadDto>> GetStandingsAsync(int tournamentId, string? groupName = null)
        {
            var query = _context.TeamRegistrations
                .Include(r => r.Team)
                .Where(r => r.TournamentId == tournamentId && r.ApprovalStatus == "Approved");

            if (!string.IsNullOrEmpty(groupName))
            {
                query = query.Where(r => r.GroupName == groupName);
            }

            var list = await query
                .OrderByDescending(r => r.Points)
                .ThenByDescending(r => r.GoalsFor - r.GoalsAgainst)
                .ThenByDescending(r => r.GoalsFor)
                .ThenByDescending(r => r.FairPlayPoints)
                .Select(r => new StandingReadDto
                {
                    TeamId = r.TeamId,
                    TeamName = r.Team != null ? r.Team.TeamName : "",
                    LogoUrl = r.Team != null ? r.Team.LogoUrl : null,
                    GroupName = r.GroupName,
                    MatchesPlayed = r.MatchesPlayed,
                    Wins = r.Wins,
                    Draws = r.Draws,
                    Losses = r.Losses,
                    GoalsFor = r.GoalsFor,
                    GoalsAgainst = r.GoalsAgainst,
                    Points = r.Points,
                    FairPlayPoints = r.FairPlayPoints
                })
                .ToListAsync();

            for (int i = 0; i < list.Count; i++)
            {
                list[i].Rank = i + 1;
            }

            return list;
        }

        public async Task<List<TopScorerDto>> GetTopScorersAsync(int tournamentId, int top = 10)
        {
            var stats = await _context.PlayerStats
                .Where(ps => ps.TournamentId == tournamentId && ps.GoalsScored > 0)
                .OrderByDescending(ps => ps.GoalsScored)
                .ThenBy(ps => ps.YellowCards)
                .Take(top)
                .Select(ps => new TopScorerDto
                {
                    PlayerId = ps.PlayerId,
                    FullName = ps.FullName,
                    StudentCode = ps.StudentCode,
                    JerseyNumber = ps.JerseyNumber,
                    Position = ps.Position,
                    TeamId = ps.TeamId,
                    TeamName = ps.TeamName,
                    LogoUrl = ps.LogoUrl,
                    GoalsScored = ps.GoalsScored,
                    YellowCards = ps.YellowCards,
                    RedCards = ps.RedCards
                })
                .ToListAsync();

            for (int i = 0; i < stats.Count; i++)
            {
                stats[i].Rank = i + 1;
            }

            return stats;
        }
    }
}
