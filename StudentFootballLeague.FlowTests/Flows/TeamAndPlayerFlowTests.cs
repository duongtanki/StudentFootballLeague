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

public class TeamAndPlayerFlowTests
{
    [Fact]
    public async Task Team_And_Player_Lifecycle_Flow_Works()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // 1. Đội trưởng 1 đăng ký & tạo đội bóng
        var cap1 = await FlowTestHelper.RegisterTeamManagerAsync(
            client, "Captain SE", "cap.se@fpt.edu.vn", "HE180001");
        client.SetBearerToken(cap1.AccessToken);

        var createTeamResp = await client.PostAsJsonAsync("/api/teams", new TeamCreateDto
        {
            TeamName = "FC Software Engineering",
            FacultyOrClub = "Ngành Kỹ thuật phần mềm",
            HomeKitColor = "Cam - Trắng"
        });
        Assert.Equal(HttpStatusCode.Created, createTeamResp.StatusCode);
        using var teamDoc = JsonDocument.Parse(await createTeamResp.Content.ReadAsStringAsync());
        var team1Id = teamDoc.RootElement.GetProperty("teamId").GetInt32();

        // 2. Chặn tạo đội thứ 2 cho cùng 1 Đội trưởng
        var secondTeamResp = await client.PostAsJsonAsync("/api/teams", new TeamCreateDto
        {
            TeamName = "FC SE 2",
            FacultyOrClub = "SE",
            HomeKitColor = "Đỏ"
        });
        Assert.Equal(HttpStatusCode.BadRequest, secondTeamResp.StatusCode);

        // 3. Thêm cầu thủ hợp lệ vào đội 1
        var p1Resp = await client.PostAsJsonAsync($"/api/teams/{team1Id}/players", new PlayerCreateDto
        {
            StudentCode = "HE180101",
            FullName = "Nguyễn Quang Hải",
            DateOfBirth = new DateTime(2004, 4, 12),
            JerseyNumber = 19,
            Position = "MF"
        });
        Assert.Equal(HttpStatusCode.OK, p1Resp.StatusCode);
        var p1 = await p1Resp.Content.ReadFromJsonAsync<PlayerReadDto>(FlowTestHelper.JsonOptions);
        Assert.NotNull(p1);

        // 4. Chặn trùng số áo trong cùng đội
        var dupJerseyResp = await client.PostAsJsonAsync($"/api/teams/{team1Id}/players", new PlayerCreateDto
        {
            StudentCode = "HE180102",
            FullName = "Cầu thủ trùng số áo",
            DateOfBirth = new DateTime(2004, 5, 1),
            JerseyNumber = 19,
            Position = "FW"
        });
        Assert.Equal(HttpStatusCode.BadRequest, dupJerseyResp.StatusCode);

        // 5. Chặn MSSV sai định dạng
        var badCodeResp = await client.PostAsJsonAsync($"/api/teams/{team1Id}/players", new PlayerCreateDto
        {
            StudentCode = "12345ABC",
            FullName = "Sai MSSV",
            DateOfBirth = new DateTime(2004, 5, 1),
            JerseyNumber = 10,
            Position = "FW"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badCodeResp.StatusCode);

        // 6. Đội trưởng 2 tạo đội 2 & không thể thêm cầu thủ trùng MSSV với Đội 1, không thể xóa cầu thủ Đội 1
        var cap2 = await FlowTestHelper.RegisterTeamManagerAsync(
            client, "Captain AI", "cap.ai@fpt.edu.vn", "HE180002");
        client.SetBearerToken(cap2.AccessToken);

        var team2Resp = await client.PostAsJsonAsync("/api/teams", new TeamCreateDto
        {
            TeamName = "FC Artificial Intelligence",
            FacultyOrClub = "Ngành Trí tuệ nhân tạo",
            HomeKitColor = "Xanh dương"
        });
        Assert.Equal(HttpStatusCode.Created, team2Resp.StatusCode);
        using var team2Doc = JsonDocument.Parse(await team2Resp.Content.ReadAsStringAsync());
        var team2Id = team2Doc.RootElement.GetProperty("teamId").GetInt32();

        var dupStudentResp = await client.PostAsJsonAsync($"/api/teams/{team2Id}/players", new PlayerCreateDto
        {
            StudentCode = "HE180101", // Đã ở Đội 1
            FullName = "Trùng MSSV",
            DateOfBirth = new DateTime(2004, 1, 1),
            JerseyNumber = 7,
            Position = "FW"
        });
        Assert.Equal(HttpStatusCode.BadRequest, dupStudentResp.StatusCode);

        var forbiddenDeleteResp = await client.DeleteAsync($"/api/players/{p1!.PlayerId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDeleteResp.StatusCode);

        // 7. Đội trưởng 1 cập nhật và xóa mềm cầu thủ -> số áo 19 có thể tái sử dụng
        client.SetBearerToken(cap1.AccessToken);
        var updateResp = await client.PutAsJsonAsync($"/api/players/{p1.PlayerId}", new PlayerUpdateDto
        {
            StudentCode = "HE180101",
            FullName = "Nguyễn Quang Hải (C)",
            DateOfBirth = new DateTime(2004, 4, 12),
            JerseyNumber = 19,
            Position = "MF",
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var deleteResp = await client.DeleteAsync($"/api/players/{p1.PlayerId}");
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        var listResp = await client.GetFromJsonAsync<List<PlayerReadDto>>($"/api/teams/{team1Id}/players", FlowTestHelper.JsonOptions);
        Assert.NotNull(listResp);
        Assert.Empty(listResp!);

        // Tái sử dụng số áo 19 sau khi cầu thủ cũ bị xóa mềm
        var reuseJerseyResp = await client.PostAsJsonAsync($"/api/teams/{team1Id}/players", new PlayerCreateDto
        {
            StudentCode = "HE180109",
            FullName = "Tân binh số 19",
            DateOfBirth = new DateTime(2004, 6, 1),
            JerseyNumber = 19,
            Position = "GK"
        });
        Assert.Equal(HttpStatusCode.OK, reuseJerseyResp.StatusCode);
    }
}
