using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentFootballLeague.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddKnockoutBracketMechanism : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Matches_DifferentTeams",
                table: "Matches");

            migrationBuilder.AddColumn<int>(
                name: "ChampionTeamId",
                table: "Tournaments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupCount",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "HasThirdPlaceMatch",
                table: "Tournaments",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "KnockoutTeamCount",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AlterColumn<int>(
                name: "HomeTeamId",
                table: "Matches",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "AwayTeamId",
                table: "Matches",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AwaySourceLabel",
                table: "Matches",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BracketOrder",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                table: "Matches",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeSourceLabel",
                table: "Matches",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KnockoutRound",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoserNextMatchId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoserNextMatchSlot",
                table: "Matches",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextMatchId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextMatchSlot",
                table: "Matches",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Matches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Group");

            migrationBuilder.AddColumn<int>(
                name: "WinnerTeamId",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_ChampionTeamId",
                table: "Tournaments",
                column: "ChampionTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_LoserNextMatchId",
                table: "Matches",
                column: "LoserNextMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_NextMatchId",
                table: "Matches",
                column: "NextMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_WinnerTeamId",
                table: "Matches",
                column: "WinnerTeamId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matches_DifferentTeams",
                table: "Matches",
                sql: "([HomeTeamId] IS NULL OR [AwayTeamId] IS NULL OR [HomeTeamId] <> [AwayTeamId])");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Matches_LoserNextMatchId",
                table: "Matches",
                column: "LoserNextMatchId",
                principalTable: "Matches",
                principalColumn: "MatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Matches_NextMatchId",
                table: "Matches",
                column: "NextMatchId",
                principalTable: "Matches",
                principalColumn: "MatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Teams_WinnerTeamId",
                table: "Matches",
                column: "WinnerTeamId",
                principalTable: "Teams",
                principalColumn: "TeamId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Teams_ChampionTeamId",
                table: "Tournaments",
                column: "ChampionTeamId",
                principalTable: "Teams",
                principalColumn: "TeamId",
                onDelete: ReferentialAction.Restrict);

            // Backfill dữ liệu hiện có cho cấu hình giải và các trận Knockout mẫu
            migrationBuilder.Sql(@"
                UPDATE Tournaments SET GroupCount = 4, KnockoutTeamCount = 8 WHERE MaxTeams = 16;

                UPDATE Matches
                SET Stage = 'Knockout',
                    KnockoutRound = CASE
                        WHEN RoundName LIKE N'%1/8%' THEN 16
                        WHEN RoundName LIKE N'%Tứ%' THEN 8
                        WHEN RoundName LIKE N'%Bán%' THEN 4
                        WHEN RoundName LIKE N'%Hạng 3%' THEN 3
                        ELSE 2
                    END,
                    BranchName = CASE
                        WHEN RoundName LIKE N'%Chung%' THEN N'Chung Kết'
                        WHEN RoundName LIKE N'%Hạng 3%' THEN N'Tranh Hạng 3'
                        ELSE N'Nhánh 1'
                    END
                WHERE GroupName IS NULL;

                -- Giải 1: gán BracketOrder & BranchName cho 2 trận Bán kết mẫu (13, 14)
                UPDATE Matches SET BranchName = N'Nhánh 1', BracketOrder = 1 WHERE MatchId = 13 AND TournamentId = 1;
                UPDATE Matches SET BranchName = N'Nhánh 2', BracketOrder = 2 WHERE MatchId = 14 AND TournamentId = 1;

                -- Giải 3: liên kết Bán Kết (21, 22) -> Chung Kết (23) và gán WinnerTeamId / ChampionTeamId
                UPDATE Matches
                SET WinnerTeamId = CASE
                    WHEN HomeScore > AwayScore THEN HomeTeamId
                    WHEN AwayScore > HomeScore THEN AwayTeamId
                    WHEN HomePenalty > AwayPenalty THEN HomeTeamId
                    WHEN AwayPenalty > HomePenalty THEN AwayTeamId
                    ELSE NULL
                END
                WHERE Stage = 'Knockout' AND Status = 'Completed';

                UPDATE Matches SET BranchName = N'Nhánh 1', BracketOrder = 1, NextMatchId = 23, NextMatchSlot = 'Home' WHERE MatchId = 21 AND TournamentId = 3;
                UPDATE Matches SET BranchName = N'Nhánh 2', BracketOrder = 2, NextMatchId = 23, NextMatchSlot = 'Away' WHERE MatchId = 22 AND TournamentId = 3;
                UPDATE Matches SET BranchName = N'Chung Kết', BracketOrder = 1, HomeSourceLabel = N'Thắng Bán Kết 1', AwaySourceLabel = N'Thắng Bán Kết 2' WHERE MatchId = 23 AND TournamentId = 3;

                UPDATE t
                SET t.ChampionTeamId = m.WinnerTeamId
                FROM Tournaments t
                INNER JOIN Matches m ON m.TournamentId = t.TournamentId
                WHERE m.Stage = 'Knockout' AND m.KnockoutRound = 2 AND m.Status = 'Completed' AND m.WinnerTeamId IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Matches_LoserNextMatchId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Matches_NextMatchId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Teams_WinnerTeamId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Teams_ChampionTeamId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_ChampionTeamId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Matches_LoserNextMatchId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_NextMatchId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_WinnerTeamId",
                table: "Matches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matches_DifferentTeams",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ChampionTeamId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "GroupCount",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "HasThirdPlaceMatch",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "KnockoutTeamCount",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "AwaySourceLabel",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "BracketOrder",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "BranchName",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "HomeSourceLabel",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "KnockoutRound",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LoserNextMatchId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LoserNextMatchSlot",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "NextMatchId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "NextMatchSlot",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "WinnerTeamId",
                table: "Matches");

            migrationBuilder.AlterColumn<int>(
                name: "HomeTeamId",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AwayTeamId",
                table: "Matches",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matches_DifferentTeams",
                table: "Matches",
                sql: "[HomeTeamId] <> [AwayTeamId]");
        }
    }
}
