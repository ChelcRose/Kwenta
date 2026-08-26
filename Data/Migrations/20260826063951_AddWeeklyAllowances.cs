using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kwenta.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyAllowances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeeklyAllowanceId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeeklyAllowances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    AmountReceived = table.Column<decimal>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyAllowances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyAllowanceAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WeeklyAllowanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    FinancialAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyAllowanceAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyAllowanceAllocations_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeeklyAllowanceAllocations_WeeklyAllowances_WeeklyAllowanceId",
                        column: x => x.WeeklyAllowanceId,
                        principalTable: "WeeklyAllowances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_WeeklyAllowanceId",
                table: "Transactions",
                column: "WeeklyAllowanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAllowanceAllocations_FinancialAccountId",
                table: "WeeklyAllowanceAllocations",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAllowanceAllocations_WeeklyAllowanceId_FinancialAccountId",
                table: "WeeklyAllowanceAllocations",
                columns: new[] { "WeeklyAllowanceId", "FinancialAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAllowances_UserId",
                table: "WeeklyAllowances",
                column: "UserId",
                unique: true,
                filter: "\"IsActive\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_WeeklyAllowances_WeeklyAllowanceId",
                table: "Transactions",
                column: "WeeklyAllowanceId",
                principalTable: "WeeklyAllowances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_WeeklyAllowances_WeeklyAllowanceId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "WeeklyAllowanceAllocations");

            migrationBuilder.DropTable(
                name: "WeeklyAllowances");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_WeeklyAllowanceId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "WeeklyAllowanceId",
                table: "Transactions");
        }
    }
}
