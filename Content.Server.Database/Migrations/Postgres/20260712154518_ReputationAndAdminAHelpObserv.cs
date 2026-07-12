using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ReputationAndAdminAHelpObserv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_ahelp_observ",
                columns: table => new
                {
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_ahelps = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_ahelp_observ", x => x.admin_user_id);
                });

            migrationBuilder.CreateTable(
                name: "reputation_votes",
                columns: table => new
                {
                    reputation_votes_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    target_kind = table.Column<byte>(type: "smallint", nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_name_snapshot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    voter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_name_snapshot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<short>(type: "smallint", nullable: false),
                    comment = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    round_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delete_reason = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reputation_votes", x => x.reputation_votes_id);
                    table.ForeignKey(
                        name: "FK_reputation_votes_round_round_id",
                        column: x => x.round_id,
                        principalTable: "round",
                        principalColumn: "round_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_reputation_votes_deleted",
                table: "reputation_votes",
                column: "deleted");

            migrationBuilder.CreateIndex(
                name: "IX_reputation_votes_round_id",
                table: "reputation_votes",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "IX_reputation_votes_target_kind_target_user_id",
                table: "reputation_votes",
                columns: new[] { "target_kind", "target_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reputation_votes_voter_user_id_target_kind_target_user_id_d~",
                table: "reputation_votes",
                columns: new[] { "voter_user_id", "target_kind", "target_user_id", "deleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_ahelp_observ");

            migrationBuilder.DropTable(
                name: "reputation_votes");
        }
    }
}
