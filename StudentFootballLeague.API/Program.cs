using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.OpenApi.Models;
using StudentFootballLeague.BusinessLogic.Services;
using StudentFootballLeague.DataAccess.Data;
using StudentFootballLeague.DataAccess.Entities;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình DbContext với SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình Dependency Injection cho Business Services
builder.Services.AddScoped<ITokenService>(sp =>
{
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "StudentFootballLeagueSecretKey2026_PRN232_Final_Project_SuperSecureKey!@#";
    var issuer = builder.Configuration["Jwt:Issuer"] ?? "StudentFootballLeagueAPI";
    var audience = builder.Configuration["Jwt:Audience"] ?? "StudentFootballLeagueClient";
    var duration = int.TryParse(builder.Configuration["Jwt:DurationInMinutes"], out var d) ? d : 180;
    return new TokenService(jwtKey, issuer, audience, duration);
});

builder.Services.AddScoped<IStandingsService, StandingsService>();
builder.Services.AddScoped<IKnockoutService, KnockoutService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();

// 3. Cấu hình OData EDM Model
static IEdmModel GetEdmModel()
{
    var odataBuilder = new ODataConventionModelBuilder();
    odataBuilder.EntitySet<Team>("Teams");
    odataBuilder.EntitySet<Player>("Players");
    odataBuilder.EntitySet<Match>("Matches");
    var stat = odataBuilder.EntitySet<PlayerStat>("PlayerStats");
    stat.EntityType.HasKey(ps => new { ps.PlayerId, ps.TournamentId });
    return odataBuilder.GetEdmModel();
}

// 4. Cấu hình Controllers + OData v8
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    })
    .AddOData(opt => opt
        .Select()
        .Filter()
        .OrderBy()
        .Expand()
        .Count()
        .SetMaxTop(100)
        .AddRouteComponents("odata", GetEdmModel())
    );

// 5. Cấu hình JWT Bearer Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "StudentFootballLeagueSecretKey2026_PRN232_Final_Project_SuperSecureKey!@#";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "StudentFootballLeagueAPI",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "StudentFootballLeagueClient",
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
    };
});

builder.Services.AddAuthorization();

// 6. Cấu hình CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 7. Cấu hình Swagger với JWT Bearer & Xử lý xung đột OData
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Student Football League Management API",
        Version = "v1",
        Description = "ASP.NET Core Web API & OData Services cho Quản lý Giải bóng đá Sinh viên - PRN232"
    });

    // Giải quyết xung đột route giữa RESTful và OData
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập JWT Bearer token theo định dạng: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Kích hoạt Swagger UI cho mọi môi trường (kể cả khi không đặt ASPNETCORE_ENVIRONMENT)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Student Football League API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
