using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanInterestAccrual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "InterestAccruedThrough",
                schema: "banking",
                table: "loans",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Existing loans have not had any interest booked yet, so they accrue from the day they were opened
            migrationBuilder.Sql("UPDATE banking.loans SET \"InterestAccruedThrough\" = \"OpenedAt\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterestAccruedThrough",
                schema: "banking",
                table: "loans");
        }
    }
}
