using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class DynamicMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dynamic_market",
                columns: table => new
                {
                    protoid = table.Column<string>(type: "text", nullable: false),
                    baseprice = table.Column<double>(type: "double precision", nullable: false),
                    modprice = table.Column<double>(type: "double precision", nullable: false),
                    sold_units = table.Column<long>(type: "bigint", nullable: false),
                    bought_units = table.Column<long>(type: "bigint", nullable: false),
                    last_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dynamic_market", x => x.protoid);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dynamic_market");
        }
    }
}
