using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kwenta.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowanceIncomeCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "IsDefault", "Name", "Type", "UserId" },
                values: new object[] { 9, true, "Allowance", 2, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 9);
        }
    }
}
