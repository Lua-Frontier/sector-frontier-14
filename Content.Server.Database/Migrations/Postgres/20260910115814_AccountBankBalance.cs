using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AccountBankBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "bank_balance",
                table: "preference",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE preference AS pref
                SET bank_balance = COALESCE((
                    SELECT SUM(p.bank_balance)
                    FROM profile AS p
                    WHERE p.preference_id = pref.preference_id
                ), 0);
                """);

            migrationBuilder.Sql(
                """
                UPDATE profile
                SET bank_balance = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE profile AS p
                SET bank_balance = pref.bank_balance
                FROM preference AS pref
                WHERE p.preference_id = pref.preference_id;
                """);

            migrationBuilder.DropColumn(
                name: "bank_balance",
                table: "preference");
        }
    }
}
