using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.FlowTests.Infrastructure;
using Xunit;

namespace StudentFootballLeague.FlowTests.Flows;

public class AuthFlowTests
{
    [Fact]
    public async Task Auth_Register_Login_Me_And_RBAC_Flow_Works()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // 1. Truy cập /api/auth/me khi chưa đăng nhập -> 401
        var unauthResp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);

        // 2. Đăng ký tài khoản TeamManager mới -> 200 + JWT
        var registerDto = new RegisterRequestDto
        {
            FullName = "Trần Văn Thắng",
            Email = "thangtv@fpt.edu.vn",
            Password = "password123",
            PhoneNumber = "0988776655",
            StudentCode = "HE189999",
            Role = "TeamManager"
        };
        var regResp = await client.PostAsJsonAsync("/api/auth/register", registerDto);
        Assert.Equal(HttpStatusCode.OK, regResp.StatusCode);

        var regAuth = await regResp.Content.ReadFromJsonAsync<AuthResponseDto>(FlowTestHelper.JsonOptions);
        Assert.NotNull(regAuth);
        Assert.False(string.IsNullOrWhiteSpace(regAuth!.AccessToken));
        Assert.Equal("TeamManager", regAuth.Role);

        // 3. Đăng ký trùng Email hoặc MSSV -> 400
        var dupEmailResp = await client.PostAsJsonAsync("/api/auth/register", registerDto);
        Assert.Equal(HttpStatusCode.BadRequest, dupEmailResp.StatusCode);

        var dupCodeDto = new RegisterRequestDto
        {
            FullName = "Người Khác",
            Email = "other@fpt.edu.vn",
            Password = "password123",
            PhoneNumber = "0988776644",
            StudentCode = "HE189999",
            Role = "TeamManager"
        };
        var dupCodeResp = await client.PostAsJsonAsync("/api/auth/register", dupCodeDto);
        Assert.Equal(HttpStatusCode.BadRequest, dupCodeResp.StatusCode);

        // 4. Kiểm tra /api/auth/me với Bearer Token
        client.SetBearerToken(regAuth.AccessToken);
        var meResp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
        using var meDoc = JsonDocument.Parse(await meResp.Content.ReadAsStringAsync());
        Assert.Equal("thangtv@fpt.edu.vn", meDoc.RootElement.GetProperty("email").GetString());

        // 5. TeamManager gọi API chỉ dành cho Admin -> 403 Forbidden
        var forbiddenResp = await client.PostAsJsonAsync("/api/tournaments", new TournamentCreateDto
        {
            Name = "Unauthorized Tour",
            Season = "Fall 2026",
            StartDate = System.DateTime.UtcNow.AddDays(5),
            EndDate = System.DateTime.UtcNow.AddDays(20),
            RegistrationDeadline = System.DateTime.UtcNow.AddDays(3)
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);

        // 6. Đăng nhập Admin sai mật khẩu -> 401
        client.SetBearerToken(null);
        var wrongPassResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = "admin@fpt.edu.vn",
            Password = "WrongPassword999"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassResp.StatusCode);

        // 7. Đăng nhập Admin đúng mật khẩu -> 200
        var adminAuth = await FlowTestHelper.LoginAsync(client, "admin@fpt.edu.vn");
        Assert.Equal("Admin", adminAuth.Role);
        Assert.False(string.IsNullOrWhiteSpace(adminAuth.AccessToken));
    }
}
