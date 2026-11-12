using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.FlowTests.Infrastructure;
using Xunit;

namespace StudentFootballLeague.FlowTests.Flows;

public class KnockoutFlowTests
{
    [Fact]
    public async Task Full_Knockout_Bracket_Generation_Advancement_And_Champion_Flow_Works()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await FlowTestHelper.LoginAsync(client, "admin@fpt.edu.vn");
        var referee = await FlowTestHelper.LoginAsync(client, "referee1@fpt.edu.vn");

        var tourId = await FlowTestHelper.CreateTournamentAsync(
            client, admin.AccessToken, "Knockout Championship 2026",
            maxTeams: 8, groupCount: 2, knockoutTeamCount: 4, hasThirdPlace: true);

        // Tạo 4 đội và duyệt vào Group A & Group B
        var teamA1 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "A1", 4001, 7);
        var teamA2 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "A2", 4002, 7);
        var teamB1 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "B1", 4003, 7);
        var teamB2 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "B2", 4004, 7);

        await FlowTestHelper.RegisterAndApproveTeamAsync(client, teamA1.Token, admin.AccessToken, tourId, "Group A");
        await FlowTestHelper.RegisterAndApproveTeamAsync(client, teamA2.Token, admin.AccessToken, tourId, "Group A");
        await FlowTestHelper.RegisterAndApproveTeamAsync(client, teamB1.Token, admin.AccessToken, tourId, "Group B");
        await FlowTestHelper.RegisterAndApproveTeamAsync(client, teamB2.Token, admin.AccessToken, tourId, "Group B");

        // 1. Lấy setup Knockout
        client.SetBearerToken(admin.AccessToken);
        var setupResp = await client.GetAsync($"/api/tournaments/{tourId}/knockout/setup");
        Assert.Equal(HttpStatusCode.OK, setupResp.StatusCode);
        var setup = await setupResp.Content.ReadFromJsonAsync<KnockoutSetupReadDto>(FlowTestHelper.JsonOptions);
        Assert.NotNull(setup);
        Assert.Equal(4, setup!.ApprovedTeams.Count);

        // 2. Tạo nhánh đấu Knockout (Bán kết -> Tranh hạng 3 & Chung kết)
        var baseTime = new DateTime(2026, 12, 1, 15, 0, 0);
        var generateDto = new GenerateKnockoutBracketDto
        {
            HasThirdPlaceMatch = true,
            Pairs = new List<KnockoutPairInputDto>
            {
                new()
                {
                    BracketOrder = 1,
                    BranchName = "Nhánh 1",
                    HomeTeamId = teamA1.TeamId,
                    AwayTeamId = teamB2.TeamId,
                    HomeSourceLabel = "Nhất Bảng A",
                    AwaySourceLabel = "Nhì Bảng B",
                    MatchDateTime = baseTime,
                    PitchName = "Sân 1",
                    RefereeId = referee.UserId
                },
                new()
                {
                    BracketOrder = 2,
                    BranchName = "Nhánh 2",
                    HomeTeamId = teamB1.TeamId,
                    AwayTeamId = teamA2.TeamId,
                    HomeSourceLabel = "Nhất Bảng B",
                    AwaySourceLabel = "Nhì Bảng A",
                    MatchDateTime = baseTime.AddHours(2),
                    PitchName = "Sân 1",
                    RefereeId = referee.UserId
                }
            },
            SubsequentRounds = new List<KnockoutRoundScheduleConfigDto>
            {
                new() { KnockoutRound = 3, DefaultDateTime = baseTime.AddDays(2), DefaultPitchName = "Sân Trung Tâm" },
                new() { KnockoutRound = 2, DefaultDateTime = baseTime.AddDays(2).AddHours(3), DefaultPitchName = "Sân Trung Tâm" }
            }
        };

        var genResp = await client.PostAsJsonAsync($"/api/tournaments/{tourId}/knockout/generate", generateDto);
        Assert.Equal(HttpStatusCode.OK, genResp.StatusCode);
        var bracket = await genResp.Content.ReadFromJsonAsync<KnockoutBracketReadDto>(FlowTestHelper.JsonOptions);
        Assert.NotNull(bracket);
        Assert.Equal(3, bracket!.Rounds.Count); // Bán kết (4), Hạng 3 (3), Chung kết (2)

        var semiRound = bracket.Rounds.First(r => r.KnockoutRound == 4);
        Assert.Equal(2, semiRound.Matches.Count);
        var sf1 = semiRound.Matches[0];
        var sf2 = semiRound.Matches[1];

        // 3. Trọng tài cập nhật kết quả Bán kết 1: teamA1 thắng 3-1
        client.SetBearerToken(referee.AccessToken);
        var sf1Resp = await client.PutAsJsonAsync($"/api/matches/{sf1.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 3,
            AwayScore = 1,
            Status = "Completed"
        });
        Assert.Equal(HttpStatusCode.OK, sf1Resp.StatusCode);

        // 4. Trọng tài cập nhật kết quả Bán kết 2: Hòa 1-1, Penalty teamA2 thắng 4-3
        var sf2Resp = await client.PutAsJsonAsync($"/api/matches/{sf2.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 1,
            AwayScore = 1,
            HomePenalty = 3,
            AwayPenalty = 4,
            Status = "Completed"
        });
        Assert.Equal(HttpStatusCode.OK, sf2Resp.StatusCode);

        // 5. Kiểm tra đội thắng tự động vào Chung kết, đội thua tự động xuống Tranh hạng 3
        var midBracket = await client.GetFromJsonAsync<KnockoutBracketReadDto>(
            $"/api/tournaments/{tourId}/knockout/bracket", FlowTestHelper.JsonOptions);
        var thirdPlaceMatch = midBracket!.Rounds.First(r => r.KnockoutRound == 3).Matches.Single();
        var finalMatch = midBracket.Rounds.First(r => r.KnockoutRound == 2).Matches.Single();

        Assert.Equal(teamA1.TeamId, finalMatch.HomeTeamId);
        Assert.Equal(teamA2.TeamId, finalMatch.AwayTeamId);
        Assert.Equal(teamB2.TeamId, thirdPlaceMatch.HomeTeamId);
        Assert.Equal(teamB1.TeamId, thirdPlaceMatch.AwayTeamId);

        // 6. Hoàn tất trận Tranh hạng 3 và Chung kết -> Giải đấu tự động cập nhật ChampionTeamId
        var tpResp = await client.PutAsJsonAsync($"/api/matches/{thirdPlaceMatch.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 2,
            AwayScore = 0,
            Status = "Completed"
        });
        Assert.Equal(HttpStatusCode.OK, tpResp.StatusCode);

        var finalResp = await client.PutAsJsonAsync($"/api/matches/{finalMatch.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 2,
            AwayScore = 1,
            Status = "Completed"
        });
        Assert.Equal(HttpStatusCode.OK, finalResp.StatusCode);

        var finalBracket = await client.GetFromJsonAsync<KnockoutBracketReadDto>(
            $"/api/tournaments/{tourId}/knockout/bracket", FlowTestHelper.JsonOptions);
        Assert.Equal(teamA1.TeamId, finalBracket!.ChampionTeamId);
    }
}
