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

public class MatchAndStandingsFlowTests
{
    [Fact]
    public async Task Match_Scheduling_Conflicts_Events_Standings_And_TopScorers_Flow_Works()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await FlowTestHelper.LoginAsync(client, "admin@fpt.edu.vn");
        var referee = await FlowTestHelper.LoginAsync(client, "referee1@fpt.edu.vn");

        var tourId = await FlowTestHelper.CreateTournamentAsync(client, admin.AccessToken, "Group Stage Flow Cup");

        // Tạo 3 đội và duyệt vào Group A
        var team1 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "SE", 3001, 7);
        var team2 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "AI", 3002, 7);
        var team3 = await FlowTestHelper.CreateTeamWithRosterAsync(client, "IA", 3003, 7);

        await FlowTestHelper.RegisterAndApproveTeamAsync(client, team1.Token, admin.AccessToken, tourId, "Group A");
        await FlowTestHelper.RegisterAndApproveTeamAsync(client, team2.Token, admin.AccessToken, tourId, "Group A");
        await FlowTestHelper.RegisterAndApproveTeamAsync(client, team3.Token, admin.AccessToken, tourId, "Group A");

        client.SetBearerToken(admin.AccessToken);
        var matchTime = new DateTime(2026, 11, 20, 15, 30, 0);

        // 1. Chặn xếp lịch trùng đội nhà và đội khách -> 400
        var sameTeamResp = await client.PostAsJsonAsync("/api/matches", new MatchScheduleDto
        {
            TournamentId = tourId,
            GroupName = "Group A",
            RoundName = "Vòng Bảng - Lượt 1",
            HomeTeamId = team1.TeamId,
            AwayTeamId = team1.TeamId,
            MatchDateTime = matchTime,
            PitchName = "Sân 1",
            RefereeId = referee.UserId
        });
        Assert.Equal(HttpStatusCode.BadRequest, sameTeamResp.StatusCode);

        // 2. Xếp lịch Trận 1 hợp lệ: Team 1 vs Team 2 lúc 15:30 tại Sân 1 -> 201
        var m1Resp = await client.PostAsJsonAsync("/api/matches", new MatchScheduleDto
        {
            TournamentId = tourId,
            GroupName = "Group A",
            RoundName = "Vòng Bảng - Lượt 1",
            HomeTeamId = team1.TeamId,
            AwayTeamId = team2.TeamId,
            MatchDateTime = matchTime,
            PitchName = "Sân 1",
            RefereeId = referee.UserId
        });
        Assert.Equal(HttpStatusCode.Created, m1Resp.StatusCode);
        var match1 = await m1Resp.Content.ReadFromJsonAsync<MatchReadDto>(FlowTestHelper.JsonOptions);
        Assert.NotNull(match1);

        // 3. Chặn XUNG ĐỘT SÂN trong khoảng ±90 phút (16:15 tại Sân 1) -> 400
        var pitchConflictResp = await client.PostAsJsonAsync("/api/matches", new MatchScheduleDto
        {
            TournamentId = tourId,
            GroupName = "Group A",
            RoundName = "Vòng Bảng - Lượt 2",
            HomeTeamId = team2.TeamId,
            AwayTeamId = team3.TeamId,
            MatchDateTime = matchTime.AddMinutes(45),
            PitchName = "Sân 1",
            RefereeId = referee.UserId
        });
        Assert.Equal(HttpStatusCode.BadRequest, pitchConflictResp.StatusCode);

        // 4. Chặn XUNG ĐỘT ĐỘI BÓNG trong khoảng ±90 phút (Team 1 đá lúc 16:00 ở Sân 2) -> 400
        var teamConflictResp = await client.PostAsJsonAsync("/api/matches", new MatchScheduleDto
        {
            TournamentId = tourId,
            GroupName = "Group A",
            RoundName = "Vòng Bảng - Lượt 2",
            HomeTeamId = team1.TeamId,
            AwayTeamId = team3.TeamId,
            MatchDateTime = matchTime.AddMinutes(30),
            PitchName = "Sân 2",
            RefereeId = referee.UserId
        });
        Assert.Equal(HttpStatusCode.BadRequest, teamConflictResp.StatusCode);

        // 5. Xếp lịch hợp lệ cách > 90 phút (17:15 tại Sân 1) -> 201
        var m2Resp = await client.PostAsJsonAsync("/api/matches", new MatchScheduleDto
        {
            TournamentId = tourId,
            GroupName = "Group A",
            RoundName = "Vòng Bảng - Lượt 2",
            HomeTeamId = team1.TeamId,
            AwayTeamId = team3.TeamId,
            MatchDateTime = matchTime.AddMinutes(105),
            PitchName = "Sân 1",
            RefereeId = referee.UserId
        });
        Assert.Equal(HttpStatusCode.Created, m2Resp.StatusCode);

        // 6. Trọng tài cập nhật kết quả Trận 1: kiểm tra validation phút > 120 -> 400
        client.SetBearerToken(referee.AccessToken);
        var invalidEventResp = await client.PutAsJsonAsync($"/api/matches/{match1!.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 1,
            AwayScore = 0,
            Status = "Completed",
            Events = new List<MatchEventInputDto>
            {
                new() { TeamId = team1.TeamId, PlayerId = team1.PlayerIds[5], EventType = "Goal", Minute = 140 }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidEventResp.StatusCode);

        // 7. Trọng tài cập nhật kết quả Trận 1 hợp lệ: Team 1 thắng 2-1, có thẻ vàng & thẻ đỏ -> 200
        var updateResultResp = await client.PutAsJsonAsync($"/api/matches/{match1.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 2,
            AwayScore = 1,
            Status = "Completed",
            Events = new List<MatchEventInputDto>
            {
                new() { TeamId = team1.TeamId, PlayerId = team1.PlayerIds[5], EventType = "Goal", Minute = 15, Description = "Mở tỷ số" },
                new() { TeamId = team1.TeamId, PlayerId = team1.PlayerIds[5], EventType = "Goal", Minute = 40, Description = "Cú đúp" },
                new() { TeamId = team2.TeamId, PlayerId = team2.PlayerIds[6], EventType = "Goal", Minute = 60, Description = "Gỡ 1 bàn" },
                new() { TeamId = team1.TeamId, PlayerId = team1.PlayerIds[2], EventType = "YellowCard", Minute = 55 },
                new() { TeamId = team2.TeamId, PlayerId = team2.PlayerIds[3], EventType = "RedCard", Minute = 82 }
            }
        });
        Assert.Equal(HttpStatusCode.OK, updateResultResp.StatusCode);

        // 8. Kiểm tra Bảng xếp hạng tự động cập nhật điểm & Fair-Play
        var standings = await client.GetFromJsonAsync<List<StandingReadDto>>(
            $"/api/tournaments/{tourId}/standings?groupName=Group A", FlowTestHelper.JsonOptions);
        Assert.NotNull(standings);
        var s1 = standings!.First(s => s.TeamId == team1.TeamId);
        var s2 = standings.First(s => s.TeamId == team2.TeamId);

        Assert.Equal(1, s1.MatchesPlayed);
        Assert.Equal(3, s1.Points);
        Assert.Equal(1, s1.GoalDifference);
        Assert.Equal(-1, s1.FairPlayPoints); // 1 thẻ vàng = -1

        Assert.Equal(1, s2.MatchesPlayed);
        Assert.Equal(0, s2.Points);
        Assert.Equal(-1, s2.GoalDifference);
        Assert.Equal(-3, s2.FairPlayPoints); // 1 thẻ đỏ = -3

        // 9. Kiểm tra Vua phá lưới (V_PlayerStats)
        var scorers = await client.GetFromJsonAsync<List<TopScorerDto>>(
            $"/api/tournaments/{tourId}/top-scorers?top=5", FlowTestHelper.JsonOptions);
        Assert.NotNull(scorers);
        Assert.Equal(2, scorers!.Count);
        Assert.Equal(team1.PlayerIds[5], scorers[0].PlayerId);
        Assert.Equal(2, scorers[0].GoalsScored);

        // 10. Xóa 1 sự kiện bàn thắng -> tự động giảm tỷ số (2-1 -> 1-1) & tính lại BXH (Hòa: 1 điểm mỗi đội)
        var matchDetail = await client.GetFromJsonAsync<MatchReadDto>($"/api/matches/{match1.MatchId}", FlowTestHelper.JsonOptions);
        var firstGoalEvent = matchDetail!.Events.First(e => e.TeamId == team1.TeamId && e.EventType == "Goal");

        var delEventResp = await client.DeleteAsync($"/api/matches/{match1.MatchId}/events/{firstGoalEvent.EventId}");
        Assert.Equal(HttpStatusCode.OK, delEventResp.StatusCode);

        var updatedStandings = await client.GetFromJsonAsync<List<StandingReadDto>>(
            $"/api/tournaments/{tourId}/standings?groupName=Group A", FlowTestHelper.JsonOptions);
        Assert.NotNull(updatedStandings);
        Assert.Equal(1, updatedStandings!.First(s => s.TeamId == team1.TeamId).Points);
        Assert.Equal(1, updatedStandings.First(s => s.TeamId == team2.TeamId).Points);
    }

    [Fact]
    public async Task Event_Driven_Score_Calculation_And_OwnGoal_Flow_Works()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await FlowTestHelper.LoginAsync(client, "admin@fpt.edu.vn");
        var referee = await FlowTestHelper.LoginAsync(client, "referee1@fpt.edu.vn");

        var tourId = await FlowTestHelper.CreateTournamentAsync(client, admin.AccessToken, "Event Driven Score Cup");

        var teamA = await FlowTestHelper.CreateTeamWithRosterAsync(client, "TEAM_A", 4001, 7);
        var teamB = await FlowTestHelper.CreateTeamWithRosterAsync(client, "TEAM_B", 4002, 7);

        await FlowTestHelper.RegisterAndApproveTeamAsync(client, teamA.Token, admin.AccessToken, tourId, "Group A");
        await FlowTestHelper.RegisterAndApproveTeamAsync(client, teamB.Token, admin.AccessToken, tourId, "Group A");

        client.SetBearerToken(admin.AccessToken);
        var mResp = await client.PostAsJsonAsync("/api/matches", new MatchScheduleDto
        {
            TournamentId = tourId,
            GroupName = "Group A",
            RoundName = "Vòng Bảng - Lượt 1",
            HomeTeamId = teamA.TeamId,
            AwayTeamId = teamB.TeamId,
            MatchDateTime = new DateTime(2026, 11, 25, 14, 0, 0),
            PitchName = "Sân 3",
            RefereeId = referee.UserId
        });
        Assert.Equal(HttpStatusCode.Created, mResp.StatusCode);
        var match = await mResp.Content.ReadFromJsonAsync<MatchReadDto>(FlowTestHelper.JsonOptions);
        Assert.NotNull(match);

        // 1. Trọng tài cập nhật danh sách sự kiện:
        // - Team A ghi 1 bàn (Goal)
        // - Team A phản lưới nhà 1 bàn (OwnGoal) -> tính cho Team B
        // - Team B ghi 1 bàn (PenaltyGoal) -> tính cho Team B
        // -> Kết quả thực tế phải là: Team A 1 - 2 Team B (dù client có gửi HomeScore 0 AwayScore 0 thì server vẫn tự tính ra 1-2)
        client.SetBearerToken(referee.AccessToken);
        var resultResp = await client.PutAsJsonAsync($"/api/matches/{match!.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 0, // Server sẽ tự động tính từ events
            AwayScore = 0,
            Status = "InProgress",
            Events = new List<MatchEventInputDto>
            {
                new() { TeamId = teamA.TeamId, PlayerId = teamA.PlayerIds[0], EventType = "Goal", Minute = 10 },
                new() { TeamId = teamA.TeamId, PlayerId = teamA.PlayerIds[1], EventType = "OwnGoal", Minute = 30 },
                new() { TeamId = teamB.TeamId, PlayerId = teamB.PlayerIds[0], EventType = "PenaltyGoal", Minute = 65 }
            }
        });
        Assert.Equal(HttpStatusCode.OK, resultResp.StatusCode);

        var mDetail = await client.GetFromJsonAsync<MatchReadDto>($"/api/matches/{match.MatchId}", FlowTestHelper.JsonOptions);
        Assert.NotNull(mDetail);
        Assert.Equal(1, mDetail!.HomeScore); // Team A có 1 bàn thắng
        Assert.Equal(2, mDetail.AwayScore);  // Team B có 1 pen + 1 do Team A phản lưới

        // 2. Cập nhật trạng thái trận đấu thành Completed với Events = null
        // Server phải tiếp tục bảo toàn sự kiện và giữ vững tỷ số 1 - 2
        var statusResp = await client.PutAsJsonAsync($"/api/matches/{match.MatchId}/result", new MatchResultUpdateDto
        {
            HomeScore = 0, // Dù client có gửi số nào, server vẫn bắt buộc tính theo events là 1-2
            AwayScore = 0,
            Status = "Completed",
            Events = null
        });
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);

        var completedDetail = await client.GetFromJsonAsync<MatchReadDto>($"/api/matches/{match.MatchId}", FlowTestHelper.JsonOptions);
        Assert.NotNull(completedDetail);
        Assert.Equal("Completed", completedDetail!.Status);
        Assert.Equal(1, completedDetail.HomeScore);
        Assert.Equal(2, completedDetail.AwayScore);
        Assert.Equal(3, completedDetail.Events.Count);

        // 3. Xóa sự kiện phản lưới nhà (OwnGoal) -> tỷ số Team B giảm từ 2 về 1 (tỉ số thành 1 - 1)
        var ownGoalEvent = completedDetail.Events.First(e => e.EventType == "OwnGoal");
        var delResp = await client.DeleteAsync($"/api/matches/{match.MatchId}/events/{ownGoalEvent.EventId}");
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);

        var finalDetail = await client.GetFromJsonAsync<MatchReadDto>($"/api/matches/{match.MatchId}", FlowTestHelper.JsonOptions);
        Assert.NotNull(finalDetail);
        Assert.Equal(1, finalDetail!.HomeScore);
        Assert.Equal(1, finalDetail.AwayScore);
        Assert.Equal(2, finalDetail.Events.Count);
    }
}
