using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceWalletNumber",
                table: "WithdrawRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WalletNumber",
                table: "UserWallets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientPhoneNumber",
                table: "DepositRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WalletOwnerName",
                table: "DepositRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceWalletNumber",
                table: "WithdrawRequests");

            migrationBuilder.DropColumn(
                name: "WalletNumber",
                table: "UserWallets");

            migrationBuilder.DropColumn(
                name: "RecipientPhoneNumber",
                table: "DepositRequests");

            migrationBuilder.DropColumn(
                name: "WalletOwnerName",
                table: "DepositRequests");
        }
    }
}
