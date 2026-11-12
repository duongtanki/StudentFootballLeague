using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using StudentFootballLeague.BusinessLogic.DTOs;
using Xunit;

namespace StudentFootballLeague.FlowTests.Infrastructure;

public static class FlowTestHelper
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void SetBearerToken(this HttpClient client, string? token)
    {
        client.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<AuthResponseDto> LoginAsync(HttpClient client, string email, string password = "123456")
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });
        resp.EnsureSuccessStatusCode();
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(auth);
        return auth!;
    }

    public static async Task<AuthResponseDto> RegisterTeamManagerAsync(
        HttpClient client,
        string fullName,
        string email,
        string studentCode,
        string phoneNumber = "0988776655",
        string password = "password123")
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FullName = fullName,
            Email = email,
            Password = password,
            PhoneNumber = phoneNumber,
            StudentCode = studentCode,
            Role = "TeamManager"
        });
        resp.EnsureSuccessStatusCode();
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(auth);
        return auth!;
    }

    public static async Task<int> CreateTournamentAsync(
        HttpClient client,
        string adminToken,
        string name,
        int maxTeams = 8,
        int groupCount = 2,
        int knockoutTeamCount = 4,
        bool hasThirdPlace = true,
        int minPlayers = 7,
        DateTime? registrationDeadline = null)
    {
        client.SetBearerToken(adminToken);
        var startDate = DateTime.UtcNow.AddDays(5);
        var dto = new TournamentCreateDto
        {
            Name = name,
            Season = "Fall 2026",
            StartDate = startDate,
            EndDate = startDate.AddDays(25),
            RegistrationDeadline = registrationDeadline ?? DateTime.UtcNow.AddDays(3),
            MaxTeams = maxTeams,
            GroupCount = groupCount,
            KnockoutTeamCount = knockoutTeamCount,
            HasThirdPlaceMatch = hasThirdPlace,
            MinPlayersPerTeam = minPlayers,
            MaxPlayersPerTeam = 15,
            Status = "Registering"
        };

        var resp = await client.PostAsJsonAsync("/api/tournaments", dto);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("tournamentId").GetInt32();
    }

    /// <summary>
    /// Tạo tài khoản TeamManager, tạo Đội bóng, thêm đủ cầu thủ (1 GK + n-1 cầu thủ), và trả về thông tin đội + danh sách PlayerId.
    /// </summary>
    public static async Task<(string Token, int TeamId, List<int> PlayerIds)> CreateTeamWithRosterAsync(
        HttpClient client,
        string teamPrefix,
        int codeSeed,
        int playerCount = 7)
    {
        var auth = await RegisterTeamManagerAsync(
            client,
            fullName: $"Captain {teamPrefix}",
            email: $"cap.{teamPrefix.ToLowerInvariant()}.{codeSeed}@fpt.edu.vn",
            studentCode: $"HE{codeSeed:D6}");

        client.SetBearerToken(auth.AccessToken);

        var teamResp = await client.PostAsJsonAsync("/api/teams", new TeamCreateDto
        {
            TeamName = $"FC {teamPrefix} {codeSeed}",
            FacultyOrClub = $"Khoa {teamPrefix}",
            HomeKitColor = "Cam - Đen"
        });
        teamResp.EnsureSuccessStatusCode();

        using var teamDoc = JsonDocument.Parse(await teamResp.Content.ReadAsStringAsync());
        var teamId = teamDoc.RootElement.GetProperty("teamId").GetInt32();

        var playerIds = new List<int>();
        string[] positions = { "GK", "DF", "DF", "MF", "MF", "FW", "FW", "MF", "DF", "FW" };

        for (int i = 0; i < playerCount; i++)
        {
            var playerResp = await client.PostAsJsonAsync($"/api/teams/{teamId}/players", new PlayerCreateDto
            {
                StudentCode = $"SE{codeSeed:D4}{i + 1:D2}",
                FullName = $"Player {teamPrefix} {i + 1}",
                DateOfBirth = new DateTime(2004, 1, 15),
                JerseyNumber = i + 1,
                Position = positions[i % positions.Length]
            });
            playerResp.EnsureSuccessStatusCode();
            var player = await playerResp.Content.ReadFromJsonAsync<PlayerReadDto>(JsonOptions);
            Assert.NotNull(player);
            playerIds.Add(player!.PlayerId);
        }

        return (auth.AccessToken, teamId, playerIds);
    }

    public static async Task<int> RegisterAndApproveTeamAsync(
        HttpClient client,
        string managerToken,
        string adminToken,
        int tournamentId,
        string groupName)
    {
        client.SetBearerToken(managerToken);
        var regResp = await client.PostAsync($"/api/tournaments/{tournamentId}/register", null);
        regResp.EnsureSuccessStatusCode();

        using var regDoc = JsonDocument.Parse(await regResp.Content.ReadAsStringAsync());
        var registrationId = regDoc.RootElement.GetProperty("registrationId").GetInt32();

        client.SetBearerToken(adminToken);
        var appResp = await client.PutAsJsonAsync($"/api/registrations/{registrationId}/approval", new RegistrationApprovalDto
        {
            Status = "Approved",
            GroupName = groupName
        });
        appResp.EnsureSuccessStatusCode();

        return registrationId;
    }
}
