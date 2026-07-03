using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContractRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ServiceProviderUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DailyRate",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "ServiceProviderUserId",
                table: "Contracts",
                newName: "ServiceProviderPhoneNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_ServiceProviderUserId",
                table: "Contracts",
                newName: "IX_Contracts_ServiceProviderPhoneNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ServiceProviderPhoneNumber",
                table: "Contracts",
                newName: "ServiceProviderUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_ServiceProviderPhoneNumber",
                table: "Contracts",
                newName: "IX_Contracts_ServiceProviderUserId");

            migrationBuilder.AddColumn<decimal>(
                name: "DailyRate",
                table: "Contracts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_ServiceProviderUserId",
                table: "Contracts",
                column: "ServiceProviderUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
