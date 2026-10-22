-- =====================================================================
-- HỆ THỐNG QUẢN LÝ GIẢI BÓNG ĐÁ SINH VIÊN (STUDENT FOOTBALL LEAGUE)
-- MÔN HỌC: PRN232 - ĐỀ TÀI SỐ 05
-- CƠ SỞ DỮ LIỆU ĐÃ TỐI ƯU HÓA RÀNG BUỘC (DDL + SEED DATA)
-- =====================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'StudentFootballLeagueDB')
BEGIN
    CREATE DATABASE StudentFootballLeagueDB;
END
GO

USE StudentFootballLeagueDB;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- Xóa View và Bảng theo thứ tự phụ thuộc khóa ngoại nếu đã tồn tại
IF OBJECT_ID('dbo.V_PlayerStats', 'V') IS NOT NULL DROP VIEW dbo.V_PlayerStats;
IF OBJECT_ID('dbo.MatchEvents', 'U') IS NOT NULL DROP TABLE dbo.MatchEvents;
IF OBJECT_ID('dbo.Matches', 'U') IS NOT NULL DROP TABLE dbo.Matches;
IF OBJECT_ID('dbo.TeamRegistrations', 'U') IS NOT NULL DROP TABLE dbo.TeamRegistrations;
IF OBJECT_ID('dbo.Players', 'U') IS NOT NULL DROP TABLE dbo.Players;
IF OBJECT_ID('dbo.Teams', 'U') IS NOT NULL DROP TABLE dbo.Teams;
IF OBJECT_ID('dbo.Tournaments', 'U') IS NOT NULL DROP TABLE dbo.Tournaments;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- =====================================================================
-- 1. BẢNG Users (Tài khoản người dùng & Phân quyền)
-- =====================================================================
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email VARCHAR(150) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    PhoneNumber VARCHAR(15) NOT NULL CONSTRAINT CK_Users_Phone CHECK (LEN(PhoneNumber) >= 10),
    StudentCode VARCHAR(20) NULL,
    Role VARCHAR(30) NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'TeamManager',
    IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin', 'TeamManager', 'Referee', 'Guest'))
);
GO

-- FILTERED UNIQUE INDEX: Cho phép nhiều tài khoản có StudentCode = NULL (như Admin, Referee)
CREATE UNIQUE NONCLUSTERED INDEX UQ_Users_StudentCode 
ON Users(StudentCode) 
WHERE StudentCode IS NOT NULL;
GO

-- =====================================================================
-- 2. BẢNG Tournaments (Mùa giải bóng đá)
-- =====================================================================
CREATE TABLE Tournaments (
    TournamentId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL CONSTRAINT UQ_Tournaments_Name UNIQUE,
    Season VARCHAR(20) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    RegistrationDeadline DATETIME2 NOT NULL,
    MaxTeams INT NOT NULL CONSTRAINT CK_Tournaments_MaxTeams CHECK (MaxTeams IN (8, 12, 16, 24, 32)),
    MinPlayersPerTeam INT NOT NULL CONSTRAINT DF_Tournaments_MinPlayers DEFAULT 7,
    MaxPlayersPerTeam INT NOT NULL CONSTRAINT DF_Tournaments_MaxPlayers DEFAULT 15,
    Status VARCHAR(30) NOT NULL CONSTRAINT DF_Tournaments_Status DEFAULT 'Registering',
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Tournaments_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT CK_Tournaments_Dates CHECK (EndDate > StartDate AND RegistrationDeadline <= StartDate),
    CONSTRAINT CK_Tournaments_Status CHECK (Status IN ('Registering', 'Ongoing', 'Completed', 'Cancelled'))
);
GO

-- =====================================================================
-- 3. BẢNG Teams (Đội bóng sinh viên)
-- =====================================================================
CREATE TABLE Teams (
    TeamId INT IDENTITY(1,1) PRIMARY KEY,
    TeamName NVARCHAR(100) NOT NULL CONSTRAINT UQ_Teams_Name UNIQUE,
    FacultyOrClub NVARCHAR(100) NOT NULL,
    LogoUrl VARCHAR(500) NULL,
    ManagerId INT NOT NULL,
    HomeKitColor NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Teams_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT FK_Teams_Users FOREIGN KEY (ManagerId) REFERENCES Users(UserId)
);
GO

-- =====================================================================
-- 4. BẢNG Players (Cầu thủ sinh viên)
-- =====================================================================
CREATE TABLE Players (
    PlayerId INT IDENTITY(1,1) PRIMARY KEY,
    TeamId INT NOT NULL,
    StudentCode VARCHAR(20) NOT NULL CONSTRAINT UQ_Players_StudentCode UNIQUE,
    FullName NVARCHAR(100) NOT NULL,
    DateOfBirth DATE NOT NULL,
    JerseyNumber INT NOT NULL CONSTRAINT CK_Players_JerseyNumber CHECK (JerseyNumber BETWEEN 1 AND 99),
    Position VARCHAR(20) NOT NULL CONSTRAINT CK_Players_Position CHECK (Position IN ('GK', 'DF', 'MF', 'FW')),
    AvatarUrl VARCHAR(500) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Players_IsActive DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Players_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT FK_Players_Teams FOREIGN KEY (TeamId) REFERENCES Teams(TeamId) ON DELETE CASCADE,
    CONSTRAINT UQ_Players_Team_Jersey UNIQUE (TeamId, JerseyNumber)
);
GO

-- =====================================================================
-- 5. BẢNG TeamRegistrations (Hồ sơ đăng ký & Bảng xếp hạng)
-- =====================================================================
CREATE TABLE TeamRegistrations (
    RegistrationId INT IDENTITY(1,1) PRIMARY KEY,
    TournamentId INT NOT NULL,
    TeamId INT NOT NULL,
    GroupName VARCHAR(20) NULL,
    ApprovalStatus VARCHAR(20) NOT NULL CONSTRAINT DF_TeamReg_Status DEFAULT 'Pending',
    RejectReason NVARCHAR(255) NULL,
    MatchesPlayed INT NOT NULL CONSTRAINT DF_TeamReg_MP DEFAULT 0,
    Wins INT NOT NULL CONSTRAINT DF_TeamReg_W DEFAULT 0,
    Draws INT NOT NULL CONSTRAINT DF_TeamReg_D DEFAULT 0,
    Losses INT NOT NULL CONSTRAINT DF_TeamReg_L DEFAULT 0,
    GoalsFor INT NOT NULL CONSTRAINT DF_TeamReg_GF DEFAULT 0,
    GoalsAgainst INT NOT NULL CONSTRAINT DF_TeamReg_GA DEFAULT 0,
    Points INT NOT NULL CONSTRAINT DF_TeamReg_Pts DEFAULT 0,
    FairPlayPoints INT NOT NULL CONSTRAINT DF_TeamReg_FP DEFAULT 0,
    RegisteredAt DATETIME2 NOT NULL CONSTRAINT DF_TeamReg_RegisteredAt DEFAULT GETDATE(),
    CONSTRAINT FK_TeamReg_Tournaments FOREIGN KEY (TournamentId) REFERENCES Tournaments(TournamentId),
    CONSTRAINT FK_TeamReg_Teams FOREIGN KEY (TeamId) REFERENCES Teams(TeamId),
    CONSTRAINT UQ_TeamReg_Tournament_Team UNIQUE (TournamentId, TeamId),
    CONSTRAINT CK_TeamReg_ApprovalStatus CHECK (ApprovalStatus IN ('Pending', 'Approved', 'Rejected'))
);
GO

-- =====================================================================
-- 6. BẢNG Matches (Lịch & Kết quả Trận đấu)
-- CÓ BỔ SUNG CỘT GroupName ĐỂ PHỤC VỤ TRUY VẤN ODATA VÀ LỌC THEO BẢNG ĐẤU
-- =====================================================================
CREATE TABLE Matches (
    MatchId INT IDENTITY(1,1) PRIMARY KEY,
    TournamentId INT NOT NULL,
    GroupName VARCHAR(20) NULL,
    RoundName NVARCHAR(50) NOT NULL,
    HomeTeamId INT NOT NULL,
    AwayTeamId INT NOT NULL,
    MatchDateTime DATETIME2 NOT NULL,
    PitchName NVARCHAR(100) NOT NULL,
    RefereeId INT NULL,
    HomeScore INT NULL CONSTRAINT CK_Matches_HomeScore CHECK (HomeScore >= 0),
    AwayScore INT NULL CONSTRAINT CK_Matches_AwayScore CHECK (AwayScore >= 0),
    HomePenalty INT NULL,
    AwayPenalty INT NULL,
    Status VARCHAR(20) NOT NULL CONSTRAINT DF_Matches_Status DEFAULT 'Scheduled',
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Matches_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT FK_Matches_Tournaments FOREIGN KEY (TournamentId) REFERENCES Tournaments(TournamentId),
    CONSTRAINT FK_Matches_HomeTeam FOREIGN KEY (HomeTeamId) REFERENCES Teams(TeamId) ON DELETE NO ACTION,
    CONSTRAINT FK_Matches_AwayTeam FOREIGN KEY (AwayTeamId) REFERENCES Teams(TeamId) ON DELETE NO ACTION,
    CONSTRAINT FK_Matches_Referee FOREIGN KEY (RefereeId) REFERENCES Users(UserId) ON DELETE SET NULL,
    CONSTRAINT CK_Matches_DifferentTeams CHECK (HomeTeamId <> AwayTeamId),
    CONSTRAINT CK_Matches_Status CHECK (Status IN ('Scheduled', 'InProgress', 'Completed', 'Postponed', 'Cancelled'))
);
GO

-- =====================================================================
-- 7. BẢNG MatchEvents (Sự kiện trận đấu: Bàn thắng & Thẻ phạt)
-- =====================================================================
CREATE TABLE MatchEvents (
    EventId INT IDENTITY(1,1) PRIMARY KEY,
    MatchId INT NOT NULL,
    TeamId INT NOT NULL,
    PlayerId INT NOT NULL,
    EventType VARCHAR(30) NOT NULL,
    Minute INT NOT NULL CONSTRAINT CK_MatchEvents_Minute CHECK (Minute BETWEEN 1 AND 120),
    Description NVARCHAR(255) NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MatchEvents_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT FK_MatchEvents_Matches FOREIGN KEY (MatchId) REFERENCES Matches(MatchId) ON DELETE CASCADE,
    CONSTRAINT FK_MatchEvents_Teams FOREIGN KEY (TeamId) REFERENCES Teams(TeamId),
    CONSTRAINT FK_MatchEvents_Players FOREIGN KEY (PlayerId) REFERENCES Players(PlayerId),
    CONSTRAINT CK_MatchEvents_EventType CHECK (EventType IN ('Goal', 'OwnGoal', 'PenaltyGoal', 'YellowCard', 'RedCard'))
);
GO

-- =====================================================================
-- 8. VIEW HỖ TRỢ TRUY VẤN VUA PHÁ LƯỚI & THỐNG KÊ (OData Support)
-- =====================================================================
CREATE VIEW V_PlayerStats AS
SELECT 
    p.PlayerId,
    p.FullName,
    p.StudentCode,
    p.JerseyNumber,
    p.Position,
    t.TeamId,
    t.TeamName,
    t.LogoUrl,
    m.TournamentId,
    COUNT(CASE WHEN me.EventType IN ('Goal', 'PenaltyGoal') THEN 1 END) AS GoalsScored,
    COUNT(CASE WHEN me.EventType = 'YellowCard' THEN 1 END) AS YellowCards,
    COUNT(CASE WHEN me.EventType = 'RedCard' THEN 1 END) AS RedCards
FROM Players p
INNER JOIN Teams t ON p.TeamId = t.TeamId
INNER JOIN MatchEvents me ON p.PlayerId = me.PlayerId
INNER JOIN Matches m ON me.MatchId = m.MatchId
GROUP BY p.PlayerId, p.FullName, p.StudentCode, p.JerseyNumber, p.Position, t.TeamId, t.TeamName, t.LogoUrl, m.TournamentId;
GO

-- =====================================================================
-- DỮ LIỆU KHỞI TẠO (SEED DATA)
-- =====================================================================

-- 1. Users (Mật khẩu hash thật BCrypt cho '123456': $2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm)
INSERT INTO Users (FullName, Email, PasswordHash, PhoneNumber, StudentCode, Role, IsActive)
VALUES 
(N'Nguyễn Văn Quản Trị (BTC)', 'admin@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0901234567', NULL, 'Admin', 1),
(N'Trần Trọng Tài Chính', 'referee1@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0902234567', NULL, 'Referee', 1),
(N'Lê Hoàng Đội Trưởng SE', 'captain.se@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0911111111', 'HE180001', 'TeamManager', 1),
(N'Phạm Minh Đội Trưởng AI', 'captain.ai@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0922222222', 'HE180002', 'TeamManager', 1),
(N'Vũ Đức Đội Trưởng IB', 'captain.ib@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0933333333', 'HS180003', 'TeamManager', 1),
(N'Hoàng Gia Đội Trưởng GD', 'captain.gd@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0944444444', 'HA180004', 'TeamManager', 1);
GO

-- 2. Tournaments
INSERT INTO Tournaments (Name, Season, StartDate, EndDate, RegistrationDeadline, MaxTeams, MinPlayersPerTeam, MaxPlayersPerTeam, Status)
VALUES 
(N'Giải Bóng đá Sinh viên FPT Cup Mùa Thu 2026', 'Fall 2026', '2026-11-01', '2026-11-30', '2026-10-28 23:59:59', 8, 7, 15, 'Ongoing'),
(N'Giải Bóng đá Tân Sinh viên Mở rộng Mùa Xuân 2027', 'Spring 2027', '2027-03-01', '2027-03-31', '2027-02-20 23:59:59', 16, 7, 15, 'Registering');
GO

-- 3. Teams
INSERT INTO Teams (TeamName, FacultyOrClub, LogoUrl, ManagerId, HomeKitColor)
VALUES 
(N'FC Kỹ Thuật Phần Mềm K18', N'Khoa Kỹ thuật Phần mềm (SE)', '/images/logos/fc-se.png', 3, N'Đỏ - Trắng'),
(N'FC Trí Tuệ Nhân Tạo AI', N'Khoa Trí tuệ Nhân tạo (AI)', '/images/logos/fc-ai.png', 4, N'Xanh Dương'),
(N'FC Kinh Doanh Quốc Tế IB', N'Khoa Quản trị Kinh doanh (IB)', '/images/logos/fc-ib.png', 5, N'Vàng - Đen'),
(N'FC Thiết Kế Đồ Họa GD', N'Khoa Thiết kế Mỹ thuật số (GD)', '/images/logos/fc-gd.png', 6, N'Trắng - Tím');
GO

-- 4. Players
INSERT INTO Players (TeamId, StudentCode, FullName, DateOfBirth, JerseyNumber, Position, IsActive)
VALUES 
-- Đội 1 (TeamId = 1)
(1, 'HE180101', N'Đặng Văn Lâm SE', '2004-05-12', 1, 'GK', 1),
(1, 'HE180102', N'Quế Ngọc Hải SE', '2004-08-20', 3, 'DF', 1),
(1, 'HE180103', N'Đoàn Văn Hậu SE', '2005-02-14', 5, 'DF', 1),
(1, 'HE180104', N'Nguyễn Quang Hải SE', '2004-04-12', 19, 'MF', 1),
(1, 'HE180105', N'Đỗ Hùng Dũng SE', '2004-09-08', 8, 'MF', 1),
(1, 'HE180106', N'Nguyễn Tiến Linh SE', '2004-10-20', 22, 'FW', 1),
(1, 'HE180107', N'Phạm Tuấn Hải SE', '2005-05-19', 9, 'FW', 1),
-- Đội 2 (TeamId = 2)
(2, 'HE180201', N'Nguyễn Filip AI', '2004-01-15', 1, 'GK', 1),
(2, 'HE180202', N'Bùi Hoàng Việt Anh AI', '2004-03-01', 20, 'DF', 1),
(2, 'HE180203', N'Nguyễn Thanh Bình AI', '2005-11-02', 6, 'DF', 1),
(2, 'HE180204', N'Nguyễn Hoàng Đức AI', '2004-01-11', 14, 'MF', 1),
(2, 'HE180205', N'Nguyễn Tuấn Anh AI', '2004-05-16', 11, 'MF', 1),
(2, 'HE180206', N'Nguyễn Văn Toàn AI', '2004-04-12', 10, 'FW', 1),
(2, 'HE180207', N'Nguyễn Đình Bắc AI', '2006-08-19', 15, 'FW', 1);
GO

-- 5. TeamRegistrations
INSERT INTO TeamRegistrations (TournamentId, TeamId, GroupName, ApprovalStatus, MatchesPlayed, Wins, Draws, Losses, GoalsFor, GoalsAgainst, Points, FairPlayPoints)
VALUES 
(1, 1, 'Group A', 'Approved', 1, 1, 0, 0, 2, 1, 3, -1),
(1, 2, 'Group A', 'Approved', 1, 0, 0, 1, 1, 2, 0, -1),
(1, 3, 'Group A', 'Approved', 0, 0, 0, 0, 0, 0, 0, 0),
(1, 4, 'Group A', 'Pending', 0, 0, 0, 0, 0, 0, 0, 0);
GO

-- 6. Matches
INSERT INTO Matches (TournamentId, GroupName, RoundName, HomeTeamId, AwayTeamId, MatchDateTime, PitchName, RefereeId, HomeScore, AwayScore, Status)
VALUES 
(1, 'Group A', N'Vòng Bảng - Lượt 1', 1, 2, '2026-11-01 15:30:00', N'Sân cỏ nhân tạo số 1 - Khu Thể chất', 2, 2, 1, 'Completed'),
(1, 'Group A', N'Vòng Bảng - Lượt 1', 3, 4, '2026-11-01 17:30:00', N'Sân cỏ nhân tạo số 1 - Khu Thể chất', 2, NULL, NULL, 'Scheduled'),
(1, 'Group A', N'Vòng Bảng - Lượt 2', 1, 3, '2026-11-05 15:30:00', N'Sân cỏ nhân tạo số 2 - Khu Thể chất', 2, NULL, NULL, 'Scheduled');
GO

-- 7. MatchEvents
INSERT INTO MatchEvents (MatchId, TeamId, PlayerId, EventType, Minute, Description)
VALUES 
(1, 1, 4, 'Goal', 18, N'Sút phạt hàng rào đẹp mắt'),
(1, 2, 13, 'Goal', 42, N'Đánh đầu cận thành gỡ hòa 1-1'),
(1, 1, 2, 'YellowCard', 55, N'Phạm lỗi ngăn cản đợt phản công'),
(1, 2, 9, 'YellowCard', 68, N'Kéo áo đối phương'),
(1, 1, 6, 'Goal', 79, N'Đệm bóng cận thành ấn định chiến thắng 2-1');
GO

PRINT N'Cơ sở dữ liệu StudentFootballLeagueDB đã được khởi tạo và nạp dữ liệu thành công!';
