using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class FirebaseTokensV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirebaseToken_AspNetUsers_userId",
                table: "FirebaseToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FirebaseToken",
                table: "FirebaseToken");

            migrationBuilder.RenameTable(
                name: "FirebaseToken",
                newName: "FirebaseTokens");

            migrationBuilder.RenameIndex(
                name: "IX_FirebaseToken_userId",
                table: "FirebaseTokens",
                newName: "IX_FirebaseTokens_userId");

            migrationBuilder.AlterColumn<string>(
                name: "userId",
                table: "FirebaseTokens",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FirebaseTokens",
                table: "FirebaseTokens",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FirebaseTokens_AspNetUsers_userId",
                table: "FirebaseTokens",
                column: "userId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirebaseTokens_AspNetUsers_userId",
                table: "FirebaseTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FirebaseTokens",
                table: "FirebaseTokens");

            migrationBuilder.RenameTable(
                name: "FirebaseTokens",
                newName: "FirebaseToken");

            migrationBuilder.RenameIndex(
                name: "IX_FirebaseTokens_userId",
                table: "FirebaseToken",
                newName: "IX_FirebaseToken_userId");

            migrationBuilder.AlterColumn<string>(
                name: "userId",
                table: "FirebaseToken",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_FirebaseToken",
                table: "FirebaseToken",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FirebaseToken_AspNetUsers_userId",
                table: "FirebaseToken",
                column: "userId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
