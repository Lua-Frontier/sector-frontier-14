using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE preference
                SET bank_balance = COALESCE((
                    SELECT SUM(p.bank_balance)
                    FROM profile AS p
                    WHERE p.preference_id = preference.preference_id
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
                UPDATE profile
                SET bank_balance = (
                    SELECT pref.bank_balance
                    FROM preference AS pref
                    WHERE pref.preference_id = profile.preference_id
                );
                """);

            migrationBuilder.DropColumn(
                name: "bank_balance",
                table: "preference");
        }
    }
}
