using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.FlowTests.Infrastructure;
using Xunit;

namespace StudentFootballLeague.FlowTests.Flows;

public class TournamentRegistrationFlowTests
{
    [Fact]
    public async Task Tournament_Creation_Validation_And_Team_Registration_Approval_Flow_Works()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var admin = await FlowTestHelper.LoginAsync(client, "admin@fpt.edu.vn");
        client.SetBearerToken(admin.AccessToken);

        // 1. Admin tạo giải đấu sai ngày tháng (EndDate <= StartDate) -> 400
        var invalidTourResp = await client.PostAsJsonAsync("/api/tournaments", new TournamentCreateDto
        {
            Name = "Invalid Dates Tour",
            Season = "Fall 2026",
            StartDate = DateTime.UtcNow.AddDays(10),
            EndDate = DateTime.UtcNow.AddDays(5),
            RegistrationDeadline = DateTime.UtcNow.AddDays(3),
            MaxTeams = 8,
            GroupCount = 2,
            KnockoutTeamCount = 4
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidTourResp.StatusCode);

        // 2. Admin tạo giải đấu hợp lệ -> 201
        var tourId = await FlowTestHelper.CreateTournamentAsync(client, admin.AccessToken, "FPTU Cup 2026");

        // 3. Admin tạo thêm 1 giải đấu đã quá hạn đăng ký
        var expiredTourId = await FlowTestHelper.CreateTournamentAsync(
            client,
            admin.AccessToken,
            "Expired Tour 2026",
            registrationDeadline: DateTime.UtcNow.AddDays(-1));

        // 4. Đội trưởng A tạo đội bóng với 7 cầu thủ nhưng KHÔNG có Thủ môn (GK)
        var capA = await FlowTestHelper.RegisterTeamManagerAsync(client, "Cap A", "capa@fpt.edu.vn", "HE181001");
        client.SetBearerToken(capA.AccessToken);

        var teamAResp = await client.PostAsJsonAsync("/api/teams", new TeamCreateDto
        {
            TeamName = "FC No GK",
            FacultyOrClub = "SE",
            HomeKitColor = "Đỏ"
        });
        teamAResp.EnsureSuccessStatusCode();
        using var teamADoc = JsonDocument.Parse(await teamAResp.Content.ReadAsStringAsync());
        var teamAId = teamADoc.RootElement.GetProperty("teamId").GetInt32();

        for (int i = 1; i <= 7; i++)
        {
            var pResp = await client.PostAsJsonAsync($"/api/teams/{teamAId}/players", new PlayerCreateDto
            {
                StudentCode = $"SE1810{i:D2}",
                FullName = $"NoGK Player {i}",
                DateOfBirth = new DateTime(2004, 2, 1),
                JerseyNumber = i,
                Position = "MF" // Cố tình không có GK
            });
            pResp.EnsureSuccessStatusCode();
        }

        // Đăng ký khi chưa có GK -> 400
        var noGkRegResp = await client.PostAsync($"/api/tournaments/{tourId}/register", null);
        Assert.Equal(HttpStatusCode.BadRequest, noGkRegResp.StatusCode);

        // Bổ sung 1 Thủ môn (GK) -> Đăng ký thành công (200)
        var gkResp = await client.PostAsJsonAsync($"/api/teams/{teamAId}/players", new PlayerCreateDto
        {
            StudentCode = "SE181099",
            FullName = "Thủ Môn Chính",
            DateOfBirth = new DateTime(2004, 2, 1),
            JerseyNumber = 99,
            Position = "GK"
        });
        gkResp.EnsureSuccessStatusCode();

        var okRegResp = await client.PostAsync($"/api/tournaments/{tourId}/register", null);
        Assert.Equal(HttpStatusCode.OK, okRegResp.StatusCode);
        using var regADoc = JsonDocument.Parse(await okRegResp.Content.ReadAsStringAsync());
        var regAId = regADoc.RootElement.GetProperty("registrationId").GetInt32();

        // Chặn đăng ký trùng 2 lần vào cùng giải -> 400
        var dupRegResp = await client.PostAsync($"/api/tournaments/{tourId}/register", null);
        Assert.Equal(HttpStatusCode.BadRequest, dupRegResp.StatusCode);

        // Chặn đăng ký vào giải đã hết hạn -> 400
        var expiredRegResp = await client.PostAsync($"/api/tournaments/{expiredTourId}/register", null);
        Assert.Equal(HttpStatusCode.BadRequest, expiredRegResp.StatusCode);

        // 5. Đội trưởng B có đủ đội hình và đăng ký
        var teamB = await FlowTestHelper.CreateTeamWithRosterAsync(client, "TeamB", 2001, 7);
        client.SetBearerToken(teamB.Token);
        var regBResp = await client.PostAsync($"/api/tournaments/{tourId}/register", null);
        Assert.Equal(HttpStatusCode.OK, regBResp.StatusCode);
        using var regBDoc = JsonDocument.Parse(await regBResp.Content.ReadAsStringAsync());
        var regBId = regBDoc.RootElement.GetProperty("registrationId").GetInt32();

        // 6. Admin duyệt Đội A vào Group A, từ chối Đội B kèm lý do
        client.SetBearerToken(admin.AccessToken);
        var approveAResp = await client.PutAsJsonAsync($"/api/registrations/{regAId}/approval", new RegistrationApprovalDto
        {
            Status = "Approved",
            GroupName = "Group A"
        });
        Assert.Equal(HttpStatusCode.OK, approveAResp.StatusCode);

        var rejectBResp = await client.PutAsJsonAsync($"/api/registrations/{regBId}/approval", new RegistrationApprovalDto
        {
            Status = "Rejected",
            RejectReason = "Hồ sơ cầu thủ chưa hợp lệ"
        });
        Assert.Equal(HttpStatusCode.OK, rejectBResp.StatusCode);

        // Kiểm tra danh sách đăng ký
        var regs = await client.GetFromJsonAsync<List<TeamRegistrationReadDto>>($"/api/registrations?tournamentId={tourId}", FlowTestHelper.JsonOptions);
        Assert.NotNull(regs);
        Assert.Equal(2, regs!.Count);
        Assert.Equal("Approved", regs.First(r => r.RegistrationId == regAId).ApprovalStatus);
        Assert.Equal("Group A", regs.First(r => r.RegistrationId == regAId).GroupName);
        Assert.Equal("Rejected", regs.First(r => r.RegistrationId == regBId).ApprovalStatus);
        Assert.Equal("Hồ sơ cầu thủ chưa hợp lệ", regs.First(r => r.RegistrationId == regBId).RejectReason);
    }
}
