using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kwenta.Migrations
{
    /// <inheritdoc />
    public partial class AddTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    FromAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transfers_FinancialAccounts_FromAccountId",
                        column: x => x.FromAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transfers_FinancialAccounts_ToAccountId",
                        column: x => x.ToAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_FromAccountId",
                table: "Transfers",
                column: "FromAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ToAccountId",
                table: "Transfers",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_UserId_Date",
                table: "Transfers",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transfers");
        }
    }
}
