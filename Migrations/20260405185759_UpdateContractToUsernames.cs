using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class UpdateContractToUsernames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ContractorId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_EngineerId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_WorkerId",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "WorkerId",
                table: "Contracts",
                newName: "WorkerUsername");

            migrationBuilder.RenameColumn(
                name: "EngineerId",
                table: "Contracts",
                newName: "EngineerUsername");

            migrationBuilder.RenameColumn(
                name: "ContractorId",
                table: "Contracts",
                newName: "ContractorUsername");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_WorkerId",
                table: "Contracts",
                newName: "IX_Contracts_WorkerUsername");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_EngineerId",
                table: "Contracts",
                newName: "IX_Contracts_EngineerUsername");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_ContractorId",
                table: "Contracts",
                newName: "IX_Contracts_ContractorUsername");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WorkerUsername",
                table: "Contracts",
                newName: "WorkerId");

            migrationBuilder.RenameColumn(
                name: "EngineerUsername",
                table: "Contracts",
                newName: "EngineerId");

            migrationBuilder.RenameColumn(
                name: "ContractorUsername",
                table: "Contracts",
                newName: "ContractorId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_WorkerUsername",
                table: "Contracts",
                newName: "IX_Contracts_WorkerId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_EngineerUsername",
                table: "Contracts",
                newName: "IX_Contracts_EngineerId");

            migrationBuilder.RenameIndex(
                name: "IX_Contracts_ContractorUsername",
                table: "Contracts",
                newName: "IX_Contracts_ContractorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_ContractorId",
                table: "Contracts",
                column: "ContractorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_EngineerId",
                table: "Contracts",
                column: "EngineerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_WorkerId",
                table: "Contracts",
                column: "WorkerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
