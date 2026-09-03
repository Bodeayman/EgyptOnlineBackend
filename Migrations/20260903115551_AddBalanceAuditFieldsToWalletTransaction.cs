using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceAuditFieldsToWalletTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BalanceAfter",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BalanceBefore",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BalanceType",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OperationType",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalanceAfter",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "BalanceBefore",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "BalanceType",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "OperationType",
                table: "WalletTransactions");
        }
    }
}
