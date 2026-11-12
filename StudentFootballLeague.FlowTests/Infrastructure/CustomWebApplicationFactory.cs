using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

namespace StudentFootballLeague.FlowTests.Infrastructure;

/// <summary>
/// WebApplicationFactory sử dụng SQLite in-memory riêng biệt cho mỗi instance,
/// đảm bảo test luồng chạy độc lập, có đầy đủ quan hệ FK, ràng buộc và View V_PlayerStats.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Database.EnsureCreated();

            // Tạo SQL View V_PlayerStats tương thích SQLite (khớp với Database/StudentFootballLeagueDB.sql)
            db.Database.ExecuteSqlRaw(@"
                CREATE VIEW IF NOT EXISTS V_PlayerStats AS
                SELECT
                    p.PlayerId,
                    p.FullName,
                    p.StudentCode,
                    p.JerseyNumber,
                    p.Position,
                    t.TeamId,
                    t.TeamName,
                    m.TournamentId,
                    COUNT(CASE WHEN me.EventType IN ('Goal', 'PenaltyGoal') THEN 1 END) AS GoalsScored,
                    COUNT(CASE WHEN me.EventType = 'YellowCard' THEN 1 END) AS YellowCards,
                    COUNT(CASE WHEN me.EventType = 'RedCard' THEN 1 END) AS RedCards
                FROM Players p
                INNER JOIN Teams t ON p.TeamId = t.TeamId
                INNER JOIN MatchEvents me ON p.PlayerId = me.PlayerId
                INNER JOIN Matches m ON me.MatchId = m.MatchId
                GROUP BY p.PlayerId, p.FullName, p.StudentCode, p.JerseyNumber, p.Position, t.TeamId, t.TeamName, m.TournamentId;
            ");

            SeedBaseAccounts(db);
        });
    }

    private static void SeedBaseAccounts(AppDbContext db)
    {
        if (db.Users.Any()) return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("123456");

        db.Users.AddRange(
            new User
            {
                FullName = "Nguyễn Văn Quản Trị (BTC)",
                Email = "admin@fpt.edu.vn",
                PasswordHash = passwordHash,
                PhoneNumber = "0901234567",
                StudentCode = null,
                Role = "Admin",
                IsActive = true
            },
            new User
            {
                FullName = "Trần Trọng Tài Chính",
                Email = "referee1@fpt.edu.vn",
                PasswordHash = passwordHash,
                PhoneNumber = "0902234567",
                StudentCode = null,
                Role = "Referee",
                IsActive = true
            }
        );

        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
