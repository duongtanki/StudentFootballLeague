using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.FlowTests.Infrastructure;
using Xunit;

namespace StudentFootballLeague.FlowTests.Flows;

public class ODataFlowTests
{
    [Fact]
    public async Task OData_Filter_OrderBy_Expand_And_PlayerStats_Queries_Work()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await FlowTestHelper.LoginAsync(client, "admin@fpt.edu.vn");
        var referee = await FlowTestHelper.LoginAsync(client, "referee1@fpt.edu.vn");

        var tourId = await FlowTestHelper.CreateTournamentAsync(client, admin.AccessToken, "OData Query Cup");
        var team1 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "Alpha", 5001, 7);
        var team2 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "Beta", 5002, 7);

        await FlowTestHelper.RegisterAndApproveTeamAsync(client, team1.Token, admin.AccessToken, tourId, "Group A");
        await FlowTestHelper.RegisterAndApproveTeamAsync(client, team2.Token, admin.AccessToken, tourId, "Group A");

        // Tạo 1 trận đấu & cập nhật bàn thắng để có dữ liệu cho Matches và V_PlayerStats
        client.SetBearerToken(admin.AccessToken);
        var mResp = await client.PostAsJsonAsync("/api/matches", new MatchScheduleDto
        {
            TournamentId = tourId,
            GroupName = "Group A",
            RoundName = "Lượt 1",
            HomeTeamId = team1.TeamId,
            AwayTeamId = team2.TeamId,
            MatchDateTime = new DateTime(2026, 11, 25, 16, 0, 0),
            PitchName = "Sân OData",
            RefereeId = referee.UserId
        });
        mResp.EnsureSuccessStatusCode();
        var match = await mResp.Content.ReadFromJsonAsync<MatchReadDto>(FlowTestHelper.JsonOptions);

        client.SetBearerToken(referee.AccessToken);
        var resResp = await client.PutAsJsonAsync($"/api/matches/{match!.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 2,
            AwayScore = 0,
            Status = "Completed",
            Events = new List<MatchEventInputDto>
            {
                new() { TeamId = team1.TeamId, PlayerId = team1.PlayerIds[5], EventType = "Goal", Minute = 10 },
                new() { TeamId = team1.TeamId, PlayerId = team1.PlayerIds[5], EventType = "Goal", Minute = 50 }
            }
        });
        resResp.EnsureSuccessStatusCode();

        // 1. OData Players: $filter + $orderby
        client.SetBearerToken(null);
        var playersResp = await client.GetAsync($"/odata/Players?$filter=TeamId eq {team1.TeamId} and Position eq 'FW'&$orderby=JerseyNumber asc");
        Assert.Equal(HttpStatusCode.OK, playersResp.StatusCode);
        using (var doc = JsonDocument.Parse(await playersResp.Content.ReadAsStringAsync()))
        {
            var values = doc.RootElement.GetProperty("value");
            Assert.True(values.GetArrayLength() >= 1);
            foreach (var item in values.EnumerateArray())
            {
                Assert.Equal("FW", item.GetProperty("Position").GetString());
                Assert.Equal(team1.TeamId, item.GetProperty("TeamId").GetInt32());
            }
        }

        // 2. OData Teams: $expand=Players + $filter=contains(...)
        var teamsResp = await client.GetAsync("/odata/Teams?$expand=Players&$filter=contains(TeamName, 'Alpha')");
        Assert.Equal(HttpStatusCode.OK, teamsResp.StatusCode);
        using (var doc = JsonDocument.Parse(await teamsResp.Content.ReadAsStringAsync()))
        {
            var values = doc.RootElement.GetProperty("value");
            Assert.Equal(1, values.GetArrayLength());
            Assert.Equal(7, values[0].GetProperty("Players").GetArrayLength());
        }

        // 3. OData Matches: $filter + $expand=HomeTeam,AwayTeam
        var matchesResp = await client.GetAsync("/odata/Matches?$filter=GroupName eq 'Group A'&$expand=HomeTeam,AwayTeam");
        Assert.Equal(HttpStatusCode.OK, matchesResp.StatusCode);
        using (var doc = JsonDocument.Parse(await matchesResp.Content.ReadAsStringAsync()))
        {
            var values = doc.RootElement.GetProperty("value");
            Assert.Equal(1, values.GetArrayLength());
            Assert.True(values[0].TryGetProperty("HomeTeam", out _));
            Assert.True(values[0].TryGetProperty("AwayTeam", out _));
        }

        // 4. OData PlayerStats (View V_PlayerStats): $filter + $orderby + $top
        var statsResp = await client.GetAsync($"/odata/PlayerStats?$filter=TournamentId eq {tourId}&$orderby=GoalsScored desc&$top=3");
        Assert.Equal(HttpStatusCode.OK, statsResp.StatusCode);
        using (var doc = JsonDocument.Parse(await statsResp.Content.ReadAsStringAsync()))
        {
            var values = doc.RootElement.GetProperty("value");
            Assert.Equal(1, values.GetArrayLength());
            Assert.Equal(2, values[0].GetProperty("GoalsScored").GetInt32());
        }
    }
}
