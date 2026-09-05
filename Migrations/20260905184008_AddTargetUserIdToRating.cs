using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetUserIdToRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetUserId",
                table: "Ratings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_TargetUserId",
                table: "Ratings",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_AspNetUsers_TargetUserId",
                table: "Ratings",
                column: "TargetUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_AspNetUsers_TargetUserId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_TargetUserId",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "Ratings");
        }
    }
}
