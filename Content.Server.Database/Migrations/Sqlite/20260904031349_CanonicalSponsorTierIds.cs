using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class CanonicalSponsorTierIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE sponsor
                SET role = CASE role
                    WHEN 'Акционер' THEN 'Shareholder'
                    WHEN 'Божество' THEN 'God'
                    WHEN 'Ранг I' THEN 'Rank1'
                    WHEN 'Ранг II' THEN 'Rank2'
                    WHEN 'Ранг III' THEN 'Rank3'
                    WHEN 'Ранг IV' THEN 'Rank4'
                    WHEN 'Ранг V' THEN 'Rank5'
                    WHEN 'Ранг VI' THEN 'Rank6'
                    WHEN 'Ранг VII' THEN 'Rank7'
                    WHEN 'Ранг VIII' THEN 'Rank8'
                    WHEN 'Ранг IX' THEN 'Rank9'
                    WHEN 'Ранг X' THEN 'Rank10'
                    ELSE role
                END
                WHERE role IN (
                    'Акционер', 'Божество',
                    'Ранг I', 'Ранг II', 'Ранг III', 'Ранг IV', 'Ранг V',
                    'Ранг VI', 'Ранг VII', 'Ранг VIII', 'Ранг IX', 'Ранг X'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE sponsor
                SET role = CASE role
                    WHEN 'Shareholder' THEN 'Акционер'
                    WHEN 'God' THEN 'Божество'
                    WHEN 'Rank1' THEN 'Ранг I'
                    WHEN 'Rank2' THEN 'Ранг II'
                    WHEN 'Rank3' THEN 'Ранг III'
                    WHEN 'Rank4' THEN 'Ранг IV'
                    WHEN 'Rank5' THEN 'Ранг V'
                    WHEN 'Rank6' THEN 'Ранг VI'
                    WHEN 'Rank7' THEN 'Ранг VII'
                    WHEN 'Rank8' THEN 'Ранг VIII'
                    WHEN 'Rank9' THEN 'Ранг IX'
                    WHEN 'Rank10' THEN 'Ранг X'
                    ELSE role
                END
                WHERE role IN (
                    'Shareholder', 'God',
                    'Rank1', 'Rank2', 'Rank3', 'Rank4', 'Rank5',
                    'Rank6', 'Rank7', 'Rank8', 'Rank9', 'Rank10'
                );
                """);
        }
    }
}
