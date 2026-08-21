using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PlayerAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_achievement",
                columns: table => new
                {
                    player_achievement_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    achievement_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    unlocked_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    reward_claimed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_achievement", x => x.player_achievement_id);
                });

            migrationBuilder.CreateTable(
                name: "player_achievement_progress",
                columns: table => new
                {
                    player_achievement_progress_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    achievement_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    progress = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_achievement_progress", x => x.player_achievement_progress_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_achievement_player_id_achievement_id",
                table: "player_achievement",
                columns: new[] { "player_id", "achievement_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_achievement_progress_player_id_achievement_id",
                table: "player_achievement_progress",
                columns: new[] { "player_id", "achievement_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_achievement");

            migrationBuilder.DropTable(
                name: "player_achievement_progress");
        }
    }
}
