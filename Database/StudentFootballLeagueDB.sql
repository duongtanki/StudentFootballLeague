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
IF OBJECT_ID('dbo.FK_Tournaments_ChampionTeam', 'F') IS NOT NULL ALTER TABLE dbo.Tournaments DROP CONSTRAINT FK_Tournaments_ChampionTeam;
IF OBJECT_ID('dbo.Tournaments', 'U') IS NOT NULL DROP TABLE dbo.Tournaments;
IF OBJECT_ID('dbo.Teams', 'U') IS NOT NULL DROP TABLE dbo.Teams;
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
    GroupCount INT NOT NULL CONSTRAINT DF_Tournaments_GroupCount DEFAULT 2,
    KnockoutTeamCount INT NOT NULL CONSTRAINT DF_Tournaments_KnockoutTeamCount DEFAULT 4,
    HasThirdPlaceMatch BIT NOT NULL CONSTRAINT DF_Tournaments_HasThirdPlace DEFAULT 1,
    ChampionTeamId INT NULL,
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
    ManagerId INT NOT NULL,
    HomeKitColor NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Teams_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT FK_Teams_Users FOREIGN KEY (ManagerId) REFERENCES Users(UserId)
);
GO

-- Thêm khóa ngoại ChampionTeamId của Tournaments tham chiếu tới Teams sau khi Teams đã được tạo
ALTER TABLE Tournaments ADD CONSTRAINT FK_Tournaments_ChampionTeam FOREIGN KEY (ChampionTeamId) REFERENCES Teams(TeamId);
GO

-- =====================================================================
-- 4. BẢNG Players (Cầu thủ sinh viên)
-- =====================================================================
CREATE TABLE Players (
    PlayerId INT IDENTITY(1,1) PRIMARY KEY,
    TeamId INT NOT NULL,
    StudentCode VARCHAR(20) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    DateOfBirth DATE NOT NULL,
    JerseyNumber INT NOT NULL CONSTRAINT CK_Players_JerseyNumber CHECK (JerseyNumber BETWEEN 1 AND 99),
    Position VARCHAR(20) NOT NULL CONSTRAINT CK_Players_Position CHECK (Position IN ('GK', 'DF', 'MF', 'FW')),
    IsActive BIT NOT NULL CONSTRAINT DF_Players_IsActive DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Players_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT FK_Players_Teams FOREIGN KEY (TeamId) REFERENCES Teams(TeamId) ON DELETE CASCADE
);
GO

-- FILTERED UNIQUE INDEX: Hỗ trợ xóa mềm (IsActive = 0) mà không khóa StudentCode hoặc số áo của cầu thủ đã rời đội
CREATE UNIQUE NONCLUSTERED INDEX UQ_Players_StudentCode
ON Players(StudentCode)
WHERE IsActive = 1;
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_Players_Team_Jersey
ON Players(TeamId, JerseyNumber)
WHERE IsActive = 1;
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
-- =====================================================================
CREATE TABLE Matches (
    MatchId INT IDENTITY(1,1) PRIMARY KEY,
    TournamentId INT NOT NULL,
    GroupName VARCHAR(20) NULL,
    RoundName NVARCHAR(50) NOT NULL,
    Stage VARCHAR(20) NOT NULL CONSTRAINT DF_Matches_Stage DEFAULT 'Group',
    KnockoutRound INT NULL,
    BranchName NVARCHAR(30) NULL,
    BracketOrder INT NULL,
    HomeSourceLabel NVARCHAR(60) NULL,
    AwaySourceLabel NVARCHAR(60) NULL,
    HomeTeamId INT NULL,
    AwayTeamId INT NULL,
    WinnerTeamId INT NULL,
    NextMatchId INT NULL,
    NextMatchSlot VARCHAR(10) NULL,
    LoserNextMatchId INT NULL,
    LoserNextMatchSlot VARCHAR(10) NULL,
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
    CONSTRAINT FK_Matches_WinnerTeam FOREIGN KEY (WinnerTeamId) REFERENCES Teams(TeamId) ON DELETE NO ACTION,
    CONSTRAINT FK_Matches_NextMatch FOREIGN KEY (NextMatchId) REFERENCES Matches(MatchId) ON DELETE NO ACTION,
    CONSTRAINT FK_Matches_LoserNextMatch FOREIGN KEY (LoserNextMatchId) REFERENCES Matches(MatchId) ON DELETE NO ACTION,
    CONSTRAINT FK_Matches_Referee FOREIGN KEY (RefereeId) REFERENCES Users(UserId) ON DELETE SET NULL,
    CONSTRAINT CK_Matches_DifferentTeams CHECK (HomeTeamId IS NULL OR AwayTeamId IS NULL OR HomeTeamId <> AwayTeamId),
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
    m.TournamentId,
    COUNT(CASE WHEN me.EventType IN ('Goal', 'PenaltyGoal') THEN 1 END) AS GoalsScored,
    COUNT(CASE WHEN me.EventType = 'YellowCard' THEN 1 END) AS YellowCards,
    COUNT(CASE WHEN me.EventType = 'RedCard' THEN 1 END) AS RedCards
FROM Players p
INNER JOIN Teams t ON p.TeamId = t.TeamId
INNER JOIN MatchEvents me ON p.PlayerId = me.PlayerId
INNER JOIN Matches m ON me.MatchId = m.MatchId
GROUP BY p.PlayerId, p.FullName, p.StudentCode, p.JerseyNumber, p.Position, t.TeamId, t.TeamName, m.TournamentId;
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
(N'Hoàng Gia Đội Trưởng GD', 'captain.gd@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0944444444', 'HA180004', 'TeamManager', 1),
(N'Nguyễn Văn Còi Vàng', 'referee2@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0903234567', NULL, 'Referee', 1),
(N'Đỗ Bảo Mật IA', 'captain.ia@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0955555555', 'HE180005', 'TeamManager', 1),
(N'Trịnh Truyền Thông MC', 'captain.mc@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0966666666', 'HS180006', 'TeamManager', 1),
(N'Lý Ngôn Ngữ JP', 'captain.jp@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0977777777', 'HJ180007', 'TeamManager', 1),
(N'Mai Thương Mại EC', 'captain.ec@fpt.edu.vn', '$2a$11$26ZvT9ABSUZnr.CO0VU9ROaqWS9Q4ZhcLNe5H/rZnu.Nt3y1tlFzm', '0988888888', 'HS180008', 'TeamManager', 1);
GO

-- 2. Teams (8 đội thuộc các chuyên ngành - Đưa lên trước Tournaments vì Tournaments có ChampionTeamId)
INSERT INTO Teams (TeamName, FacultyOrClub, ManagerId, HomeKitColor)
VALUES 
(N'FC Kỹ Thuật Phần Mềm K18', N'Khoa Kỹ thuật Phần mềm (SE)', 3, N'Đỏ - Trắng'),
(N'FC Trí Tuệ Nhân Tạo AI', N'Khoa Trí tuệ Nhân tạo (AI)', 4, N'Xanh Dương'),
(N'FC Kinh Doanh Quốc Tế IB', N'Khoa Quản trị Kinh doanh (IB)', 5, N'Vàng - Đen'),
(N'FC Thiết Kế Đồ Họa GD', N'Khoa Thiết kế Mỹ thuật số (GD)', 6, N'Trắng - Tím'),
(N'FC An Toàn Thông Tin IA', N'Khoa An toàn Thông tin (IA)', 8, N'Đen - Xanh Neon'),
(N'FC Truyền Thông MC', N'Khoa Truyền thông Đa phương tiện (MC)', 9, N'Cam - Đen'),
(N'FC Ngôn Ngữ Nhật JP', N'Khoa Ngôn ngữ Nhật (JP)', 10, N'Trắng - Đỏ'),
(N'FC Thương Mại Điện Tử EC', N'Khoa Thương mại Điện tử (EC)', 11, N'Xanh Lá - Trắng');
GO

-- 3. Tournaments
INSERT INTO Tournaments (Name, Season, StartDate, EndDate, RegistrationDeadline, MaxTeams, GroupCount, KnockoutTeamCount, HasThirdPlaceMatch, ChampionTeamId, MinPlayersPerTeam, MaxPlayersPerTeam, Status)
VALUES 
(N'Giải Bóng đá Sinh viên FPT Cup Mùa Thu 2026', 'Fall 2026', '2026-11-01', '2026-11-30', '2026-10-28 23:59:59', 8, 2, 4, 1, NULL, 7, 15, 'Ongoing'),
(N'Giải Bóng đá Tân Sinh viên Mở rộng Mùa Xuân 2027', 'Spring 2027', '2027-03-01', '2027-03-31', '2027-02-20 23:59:59', 16, 4, 8, 1, NULL, 7, 15, 'Registering'),
(N'Giải Bóng đá Campus Cup Mùa Hè 2026', 'Summer 2026', '2026-07-01', '2026-07-25', '2026-06-25 23:59:59', 8, 2, 4, 1, 1, 7, 15, 'Completed'),
(N'Giải Bóng Đá Vô Địch Mini Cup FPT 2027', 'Summer 2027', '2027-06-01', '2027-06-30', '2027-05-25 23:59:59', 8, 2, 4, 1, NULL, 7, 15, 'Registering');
GO

-- 4. Players (56 cầu thủ - mỗi đội 7 cầu thủ đủ điều kiện thi đấu)
INSERT INTO Players (TeamId, StudentCode, FullName, DateOfBirth, JerseyNumber, Position, IsActive)
VALUES 
-- Đội 1: SE (PlayerId 1..7)
(1, 'HE180101', N'Đặng Văn Lâm SE', '2004-05-12', 1, 'GK', 1),
(1, 'HE180102', N'Quế Ngọc Hải SE', '2004-08-20', 3, 'DF', 1),
(1, 'HE180103', N'Đoàn Văn Hậu SE', '2005-02-14', 5, 'DF', 1),
(1, 'HE180104', N'Nguyễn Quang Hải SE', '2004-04-12', 19, 'MF', 1),
(1, 'HE180105', N'Đỗ Hùng Dũng SE', '2004-09-08', 8, 'MF', 1),
(1, 'HE180106', N'Nguyễn Tiến Linh SE', '2004-10-20', 22, 'FW', 1),
(1, 'HE180107', N'Phạm Tuấn Hải SE', '2005-05-19', 9, 'FW', 1),
-- Đội 2: AI (PlayerId 8..14)
(2, 'HE180201', N'Nguyễn Filip AI', '2004-01-15', 1, 'GK', 1),
(2, 'HE180202', N'Bùi Hoàng Việt Anh AI', '2004-03-01', 20, 'DF', 1),
(2, 'HE180203', N'Nguyễn Thanh Bình AI', '2005-11-02', 6, 'DF', 1),
(2, 'HE180204', N'Nguyễn Hoàng Đức AI', '2004-01-11', 14, 'MF', 1),
(2, 'HE180205', N'Nguyễn Tuấn Anh AI', '2004-05-16', 11, 'MF', 1),
(2, 'HE180206', N'Nguyễn Văn Toàn AI', '2004-04-12', 10, 'FW', 1),
(2, 'HE180207', N'Nguyễn Đình Bắc AI', '2006-08-19', 15, 'FW', 1),
-- Đội 3: IB (PlayerId 15..21)
(3, 'HS180301', N'Trần Nguyên Mạnh IB', '2004-03-10', 1, 'GK', 1),
(3, 'HS180302', N'Vũ Văn Thanh IB', '2004-06-14', 2, 'DF', 1),
(3, 'HS180303', N'Hồ Tấn Tài IB', '2004-11-06', 4, 'DF', 1),
(3, 'HS180304', N'Nguyễn Hai Long IB', '2005-01-27', 7, 'MF', 1),
(3, 'HS180305', N'Lê Phạm Thành Long IB', '2004-08-05', 8, 'MF', 1),
(3, 'HS180306', N'Nguyễn Công Phượng IB', '2004-01-21', 10, 'FW', 1),
(3, 'HS180307', N'Bùi Vĩ Hào IB', '2005-02-24', 11, 'FW', 1),
-- Đội 4: GD (PlayerId 22..28)
(4, 'HA180401', N'Nguyễn Đình Triệu GD', '2004-04-04', 1, 'GK', 1),
(4, 'HA180402', N'Phạm Xuân Mạnh GD', '2004-02-09', 7, 'DF', 1),
(4, 'HA180403', N'Nguyễn Thành Chung GD', '2004-09-08', 16, 'DF', 1),
(4, 'HA180404', N'Doãn Ngọc Tân GD', '2004-08-15', 25, 'MF', 1),
(4, 'HA180405', N'Châu Ngọc Quang GD', '2005-02-01', 18, 'MF', 1),
(4, 'HA180406', N'Nguyễn Xuân Son GD', '2004-03-30', 12, 'FW', 1),
(4, 'HA180407', N'Đinh Thanh Bình GD', '2005-03-19', 9, 'FW', 1),
-- Đội 5: IA (PlayerId 29..35)
(5, 'HE180501', N'Trần Trung Kiên IA', '2005-02-09', 1, 'GK', 1),
(5, 'HE180502', N'Giáp Tuấn Dương IA', '2004-09-07', 3, 'DF', 1),
(5, 'HE180503', N'Khuất Văn Khang IA', '2005-05-11', 26, 'DF', 1),
(5, 'HE180504', N'Nguyễn Thái Sơn IA', '2005-07-13', 6, 'MF', 1),
(5, 'HE180505', N'Phan Văn Đức IA', '2004-04-11', 20, 'MF', 1),
(5, 'HE180506', N'Nhâm Mạnh Dũng IA', '2004-04-12', 19, 'FW', 1),
(5, 'HE180507', N'Nguyễn Quốc Việt IA', '2005-05-04', 9, 'FW', 1),
-- Đội 6: MC (PlayerId 36..42)
(6, 'HS180601', N'Quan Văn Chuẩn MC', '2004-01-07', 1, 'GK', 1),
(6, 'HS180602', N'Lương Duy Cương MC', '2004-11-07', 4, 'DF', 1),
(6, 'HS180603', N'Trương Tiến Anh MC', '2004-04-25', 17, 'DF', 1),
(6, 'HS180604', N'Võ Hoàng Minh Khoa MC', '2005-03-12', 8, 'MF', 1),
(6, 'HS180605', N'Lê Văn Đô MC', '2005-08-07', 14, 'MF', 1),
(6, 'HS180606', N'Nguyễn Văn Tùng MC', '2004-06-02', 9, 'FW', 1),
(6, 'HS180607', N'Hồ Thanh Minh MC', '2004-02-07', 10, 'FW', 1),
-- Đội 7: JP (PlayerId 43..49)
(7, 'HJ180701', N'Cao Văn Bình JP', '2005-01-08', 1, 'GK', 1),
(7, 'HJ180702', N'Nguyễn Đức Anh JP', '2005-05-16', 5, 'DF', 1),
(7, 'HJ180703', N'Lê Nguyên Hoàng JP', '2005-02-14', 2, 'DF', 1),
(7, 'HJ180704', N'Nguyễn Văn Trường JP', '2005-09-10', 14, 'MF', 1),
(7, 'HJ180705', N'Đinh Xuân Tiến JP', '2005-01-10', 10, 'MF', 1),
(7, 'HJ180706', N'Bùi Ngọc Long JP', '2004-10-06', 7, 'FW', 1),
(7, 'HJ180707', N'Nguyễn Ngọc Mỹ JP', '2005-02-20', 22, 'FW', 1),
-- Đội 8: EC (PlayerId 50..56)
(8, 'HS180801', N'Đoàn Huy Hoàng EC', '2005-07-31', 1, 'GK', 1),
(8, 'HS180802', N'Nguyễn Hồng Phúc EC', '2005-05-31', 3, 'DF', 1),
(8, 'HS180803', N'Nguyễn Mạnh Hưng EC', '2005-08-08', 5, 'DF', 1),
(8, 'HS180804', N'Nguyễn Công Phương EC', '2006-06-03', 8, 'MF', 1),
(8, 'HS180805', N'Thái Bá Đạt EC', '2005-03-23', 16, 'MF', 1),
(8, 'HS180806', N'Lê Đình Long Vũ EC', '2006-05-27', 11, 'FW', 1),
(8, 'HS180807', N'Nguyễn Đăng Dương EC', '2005-09-07', 9, 'FW', 1);
GO

-- 5. TeamRegistrations
INSERT INTO TeamRegistrations (TournamentId, TeamId, GroupName, ApprovalStatus)
VALUES 
-- Giải 1 (Fall 2026 - Ongoing): 8 đội chia 2 bảng
(1, 1, 'Group A', 'Approved'),
(1, 2, 'Group A', 'Approved'),
(1, 3, 'Group A', 'Approved'),
(1, 4, 'Group A', 'Approved'),
(1, 5, 'Group B', 'Approved'),
(1, 6, 'Group B', 'Approved'),
(1, 7, 'Group B', 'Approved'),
(1, 8, 'Group B', 'Approved'),
-- Giải 2 (Spring 2027 - Registering)
(2, 2, 'Group A', 'Approved'),
(2, 3, NULL, 'Pending'),
(2, 5, NULL, 'Pending'),
(2, 6, NULL, 'Pending'),
-- Giải 3 (Summer 2026 - Completed)
(3, 1, 'Group A', 'Approved'),
(3, 2, 'Group A', 'Approved'),
(3, 3, 'Group A', 'Approved'),
(3, 4, 'Group A', 'Approved'),
(3, 5, 'Group B', 'Approved'),
(3, 6, 'Group B', 'Approved'),
(3, 7, 'Group B', 'Approved'),
(3, 8, 'Group B', 'Approved'),
-- Giải 4 (Mini Cup FPT 2027 - Demo Setup: 4 đội Bảng A, 3 đội Bảng B, 1 đội Pending để demo duyệt vào Bảng B)
(4, 1, 'Group A', 'Approved'),
(4, 2, 'Group A', 'Approved'),
(4, 3, 'Group A', 'Approved'),
(4, 4, 'Group A', 'Approved'),
(4, 5, 'Group B', 'Approved'),
(4, 6, 'Group B', 'Approved'),
(4, 7, 'Group B', 'Approved'),
(4, 8, NULL, 'Pending');
GO

-- 6. Matches
INSERT INTO Matches (TournamentId, GroupName, RoundName, HomeTeamId, AwayTeamId, MatchDateTime, PitchName, RefereeId, HomeScore, AwayScore, HomePenalty, AwayPenalty, Status)
VALUES 
-- Giải 1: Lượt 1 (01/11/2026)
(1, 'Group A', N'Vòng Bảng - Lượt 1', 1, 2, '2026-11-01 15:30:00', N'Sân cỏ nhân tạo số 1 - Khu Thể chất', 2, 2, 1, NULL, NULL, 'Completed'),
(1, 'Group A', N'Vòng Bảng - Lượt 1', 3, 4, '2026-11-01 17:30:00', N'Sân cỏ nhân tạo số 1 - Khu Thể chất', 2, 3, 2, NULL, NULL, 'Completed'),
(1, 'Group B', N'Vòng Bảng - Lượt 1', 5, 6, '2026-11-01 19:00:00', N'Sân cỏ nhân tạo số 2 - Khu Thể chất', 7, 2, 0, NULL, NULL, 'Completed'),
(1, 'Group B', N'Vòng Bảng - Lượt 1', 7, 8, '2026-11-01 20:30:00', N'Sân cỏ nhân tạo số 2 - Khu Thể chất', 7, 1, 1, NULL, NULL, 'Completed'),
-- Giải 1: Lượt 2 (05/11/2026)
(1, 'Group A', N'Vòng Bảng - Lượt 2', 1, 3, '2026-11-05 15:30:00', N'Sân cỏ nhân tạo số 2 - Khu Thể chất', 2, 2, 0, NULL, NULL, 'Completed'),
(1, 'Group A', N'Vòng Bảng - Lượt 2', 2, 4, '2026-11-05 17:30:00', N'Sân cỏ nhân tạo số 2 - Khu Thể chất', 2, 1, 3, NULL, NULL, 'Completed'),
(1, 'Group B', N'Vòng Bảng - Lượt 2', 5, 7, '2026-11-05 19:00:00', N'Sân cỏ nhân tạo số 1 - Khu Thể chất', 7, 0, 2, NULL, NULL, 'Completed'),
(1, 'Group B', N'Vòng Bảng - Lượt 2', 6, 8, '2026-11-05 20:30:00', N'Sân cỏ nhân tạo số 1 - Khu Thể chất', 7, 3, 2, NULL, NULL, 'Completed'),
-- Giải 1: Lượt 3 & Bán kết sắp diễn ra
(1, 'Group A', N'Vòng Bảng - Lượt 3', 1, 4, '2026-11-12 16:00:00', N'Sân cỏ nhân tạo số 1 - Khu Thể chất', 2, 1, 1, NULL, NULL, 'InProgress'),
(1, 'Group A', N'Vòng Bảng - Lượt 3', 2, 3, '2026-11-12 18:00:00', N'Sân cỏ nhân tạo số 1 - Khu Thể chất', 7, NULL, NULL, NULL, NULL, 'Scheduled'),
(1, 'Group B', N'Vòng Bảng - Lượt 3', 5, 8, '2026-11-14 16:00:00', N'Sân cỏ nhân tạo số 2 - Khu Thể chất', 2, NULL, NULL, NULL, NULL, 'Scheduled'),
(1, 'Group B', N'Vòng Bảng - Lượt 3', 6, 7, '2026-11-14 18:00:00', N'Sân cỏ nhân tạo số 2 - Khu Thể chất', 7, NULL, NULL, NULL, NULL, 'Scheduled');

INSERT INTO Matches (TournamentId, GroupName, RoundName, Stage, KnockoutRound, BranchName, BracketOrder, HomeSourceLabel, AwaySourceLabel, HomeTeamId, AwayTeamId, WinnerTeamId, NextMatchId, NextMatchSlot, MatchDateTime, PitchName, RefereeId, HomeScore, AwayScore, HomePenalty, AwayPenalty, Status)
VALUES
(1, NULL, N'Bán Kết', 'Knockout', 4, N'Nhánh 1', 1, N'Nhất Bảng A', N'Nhì Bảng B', 1, 7, NULL, NULL, NULL, '2026-11-19 16:30:00', N'Sân vận động chính - Khu Thể chất', 2, NULL, NULL, NULL, NULL, 'Scheduled'),
(1, NULL, N'Bán Kết', 'Knockout', 4, N'Nhánh 2', 2, N'Nhất Bảng B', N'Nhì Bảng A', 4, 5, NULL, NULL, NULL, '2026-11-19 18:30:00', N'Sân vận động chính - Khu Thể chất', 7, NULL, NULL, NULL, NULL, 'Scheduled');

-- Giải 3: Summer 2026 (Completed)
INSERT INTO Matches (TournamentId, GroupName, RoundName, HomeTeamId, AwayTeamId, MatchDateTime, PitchName, RefereeId, HomeScore, AwayScore, HomePenalty, AwayPenalty, Status)
VALUES
(3, 'Group A', N'Vòng Bảng - Lượt 1', 1, 2, '2026-07-05 16:00:00', N'Sân cỏ nhân tạo số 1', 2, 3, 1, NULL, NULL, 'Completed'),
(3, 'Group A', N'Vòng Bảng - Lượt 1', 3, 4, '2026-07-05 18:00:00', N'Sân cỏ nhân tạo số 1', 7, 2, 2, NULL, NULL, 'Completed'),
(3, 'Group B', N'Vòng Bảng - Lượt 1', 5, 6, '2026-07-06 16:00:00', N'Sân cỏ nhân tạo số 2', 2, 1, 2, NULL, NULL, 'Completed'),
(3, 'Group B', N'Vòng Bảng - Lượt 1', 7, 8, '2026-07-06 18:00:00', N'Sân cỏ nhân tạo số 2', 7, 2, 0, NULL, NULL, 'Completed'),
(3, 'Group A', N'Vòng Bảng - Lượt 2', 1, 4, '2026-07-10 17:00:00', N'Sân cỏ nhân tạo số 1', 2, 2, 0, NULL, NULL, 'Completed'),
(3, 'Group B', N'Vòng Bảng - Lượt 2', 6, 7, '2026-07-10 19:00:00', N'Sân cỏ nhân tạo số 2', 7, 2, 1, NULL, NULL, 'Completed');

INSERT INTO Matches (TournamentId, GroupName, RoundName, Stage, KnockoutRound, BranchName, BracketOrder, HomeSourceLabel, AwaySourceLabel, HomeTeamId, AwayTeamId, WinnerTeamId, NextMatchId, NextMatchSlot, MatchDateTime, PitchName, RefereeId, HomeScore, AwayScore, HomePenalty, AwayPenalty, Status)
VALUES
(3, NULL, N'Bán Kết', 'Knockout', 4, N'Nhánh 1', 1, NULL, NULL, 1, 7, 1, NULL, 'Home', '2026-07-16 17:00:00', N'Sân vận động chính', 2, 3, 1, NULL, NULL, 'Completed'),
(3, NULL, N'Bán Kết', 'Knockout', 4, N'Nhánh 2', 2, NULL, NULL, 6, 2, 6, NULL, 'Away', '2026-07-16 19:00:00', N'Sân vận động chính', 7, 2, 1, NULL, NULL, 'Completed'),
(3, NULL, N'Chung Kết', 'Knockout', 2, N'Chung Kết', 1, N'Thắng Bán Kết 1', N'Thắng Bán Kết 2', 1, 6, 1, NULL, NULL, '2026-07-22 18:00:00', N'Sân vận động chính', 2, 2, 2, 4, 3, 'Completed');

UPDATE Matches SET NextMatchId = 23 WHERE MatchId IN (21, 22);
GO

-- 7. MatchEvents
INSERT INTO MatchEvents (MatchId, TeamId, PlayerId, EventType, Minute, Description)
VALUES 
-- Trận 1: SE vs AI (2-1)
(1, 1, 4, 'Goal', 18, N'Sút phạt hàng rào đẹp mắt'),
(1, 2, 13, 'Goal', 42, N'Đánh đầu cận thành gỡ hòa 1-1'),
(1, 1, 2, 'YellowCard', 55, N'Phạm lỗi ngăn cản đợt phản công'),
(1, 2, 9, 'YellowCard', 68, N'Kéo áo đối phương'),
(1, 1, 6, 'Goal', 79, N'Đệm bóng cận thành ấn định chiến thắng 2-1'),
-- Trận 2: IB vs GD (3-2)
(2, 3, 20, 'Goal', 12, N'Sút xa mở tỷ số'),
(2, 4, 27, 'Goal', 25, N'Đánh đầu gỡ hòa'),
(2, 3, 21, 'Goal', 44, N'Phản công nhanh nâng tỷ số'),
(2, 4, 27, 'PenaltyGoal', 62, N'Sút phạt đền thành công'),
(2, 4, 23, 'YellowCard', 70, N'Vào bóng quyết liệt'),
(2, 3, 20, 'Goal', 83, N'Cú đúp ấn định 3-2'),
-- Trận 3: IA vs MC (2-0)
(3, 5, 34, 'Goal', 19, N'Sút chân trái hiểm hóc'),
(3, 5, 35, 'Goal', 58, N'Bứt tốc ghi bàn'),
(3, 6, 37, 'YellowCard', 64, N'Phạm lỗi chiến thuật'),
-- Trận 4: JP vs EC (1-1)
(4, 7, 48, 'Goal', 31, N'Cú cứa lòng đẹp mắt'),
(4, 8, 56, 'Goal', 77, N'Đánh đầu gỡ hòa phút cuối'),
-- Trận 5: SE vs IB (2-0)
(5, 1, 6, 'Goal', 22, N'Dứt điểm chéo góc'),
(5, 1, 7, 'Goal', 67, N'Đệm bóng cận thành'),
(5, 3, 17, 'YellowCard', 74, N'Lỗi kéo người'),
-- Trận 6: AI vs GD (1-3)
(6, 2, 13, 'Goal', 14, N'Mở tỷ số sớm'),
(6, 4, 27, 'Goal', 38, N'Gỡ hòa 1-1'),
(6, 4, 28, 'Goal', 61, N'Lội ngược dòng'),
(6, 4, 27, 'Goal', 84, N'Hoàn tất cú đúp'),
(6, 2, 9, 'RedCard', 86, N'Thẻ đỏ trực tiếp lỗi ngăn cản cơ hội rõ rệt'),
-- Trận 7: IA vs JP (0-2)
(7, 7, 47, 'Goal', 29, N'Sút phạt trực tiếp'),
(7, 7, 48, 'Goal', 72, N'Nhân đôi cách biệt'),
-- Trận 8: MC vs EC (3-2)
(8, 6, 41, 'Goal', 10, N'Ghi bàn sớm'),
(8, 8, 55, 'Goal', 24, N'Gỡ hòa 1-1'),
(8, 6, 42, 'Goal', 49, N'Nâng tỷ số 2-1'),
(8, 8, 56, 'Goal', 65, N'San bằng 2-2'),
(8, 6, 41, 'Goal', 88, N'Bàn thắng vàng phút 88'),
-- Trận 9: SE vs GD (1-1 InProgress)
(9, 1, 6, 'Goal', 21, N'Mở tỷ số cho SE'),
(9, 4, 27, 'Goal', 39, N'Gỡ hòa cuối hiệp 1'),
-- Trận 23: Chung Kết Giải 3 SE vs MC (2-2, Pen 4-3)
(23, 1, 4, 'Goal', 15, N'Cú sút xa trái phá'),
(23, 6, 41, 'Goal', 40, N'Đánh đầu gỡ hòa'),
(23, 1, 6, 'Goal', 68, N'Đưa SE vươn lên dẫn trước'),
(23, 6, 42, 'PenaltyGoal', 89, N'Phạt đền gỡ hòa 2-2 đưa trận đấu vào luân lưu');
GO

-- 8. Tự động đồng bộ Bảng xếp hạng (TeamRegistrations) từ các trận vòng bảng
;WITH TeamMatchRows AS (
    SELECT m.TournamentId, m.GroupName, m.HomeTeamId AS TeamId, m.HomeScore AS GF, m.AwayScore AS GA
    FROM Matches m
    WHERE m.Status = 'Completed' AND m.GroupName IS NOT NULL AND m.HomeScore IS NOT NULL AND m.AwayScore IS NOT NULL
    UNION ALL
    SELECT m.TournamentId, m.GroupName, m.AwayTeamId AS TeamId, m.AwayScore AS GF, m.HomeScore AS GA
    FROM Matches m
    WHERE m.Status = 'Completed' AND m.GroupName IS NOT NULL AND m.HomeScore IS NOT NULL AND m.AwayScore IS NOT NULL
),
AggMatchStats AS (
    SELECT
        TournamentId, GroupName, TeamId,
        COUNT(*) AS MP,
        SUM(CASE WHEN GF > GA THEN 1 ELSE 0 END) AS W,
        SUM(CASE WHEN GF = GA THEN 1 ELSE 0 END) AS D,
        SUM(CASE WHEN GF < GA THEN 1 ELSE 0 END) AS L,
        SUM(GF) AS GF,
        SUM(GA) AS GA,
        SUM(CASE WHEN GF > GA THEN 3 WHEN GF = GA THEN 1 ELSE 0 END) AS Pts
    FROM TeamMatchRows
    GROUP BY TournamentId, GroupName, TeamId
),
AggCardStats AS (
    SELECT
        m.TournamentId, m.GroupName, me.TeamId,
        SUM(CASE WHEN me.EventType = 'YellowCard' THEN -1 WHEN me.EventType = 'RedCard' THEN -3 ELSE 0 END) AS FP
    FROM MatchEvents me
    INNER JOIN Matches m ON me.MatchId = m.MatchId
    WHERE m.Status = 'Completed' AND m.GroupName IS NOT NULL
    GROUP BY m.TournamentId, m.GroupName, me.TeamId
)
UPDATE tr
SET
    MatchesPlayed = ISNULL(s.MP, 0),
    Wins = ISNULL(s.W, 0),
    Draws = ISNULL(s.D, 0),
    Losses = ISNULL(s.L, 0),
    GoalsFor = ISNULL(s.GF, 0),
    GoalsAgainst = ISNULL(s.GA, 0),
    Points = ISNULL(s.Pts, 0),
    FairPlayPoints = ISNULL(c.FP, 0)
FROM TeamRegistrations tr
LEFT JOIN AggMatchStats s ON s.TournamentId = tr.TournamentId AND s.GroupName = tr.GroupName AND s.TeamId = tr.TeamId
LEFT JOIN AggCardStats c ON c.TournamentId = tr.TournamentId AND c.GroupName = tr.GroupName AND c.TeamId = tr.TeamId
WHERE tr.ApprovalStatus = 'Approved';
GO

PRINT N'Cơ sở dữ liệu StudentFootballLeagueDB đã được khởi tạo và nạp dữ liệu mẫu phong phú thành công!';
