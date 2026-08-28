using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kwenta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReimbursements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reimbursements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ReceivingAccountId = table.Column<int>(type: "integer", nullable: false),
                    ExpenseTransactionId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reimbursements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reimbursements_FinancialAccounts_ReceivingAccountId",
                        column: x => x.ReceivingAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reimbursements_Transactions_ExpenseTransactionId",
                        column: x => x.ExpenseTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reimbursements_ExpenseTransactionId",
                table: "Reimbursements",
                column: "ExpenseTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Reimbursements_ReceivingAccountId",
                table: "Reimbursements",
                column: "ReceivingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Reimbursements_UserId_Date",
                table: "Reimbursements",
                columns: new[] { "UserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reimbursements");
        }
    }
}
