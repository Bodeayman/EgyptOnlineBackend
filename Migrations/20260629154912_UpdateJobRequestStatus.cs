using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class UpdateJobRequestStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptedProviderUserId",
                table: "JobRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "JobRequests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_AcceptedProviderUserId",
                table: "JobRequests",
                column: "AcceptedProviderUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobRequests_AspNetUsers_AcceptedProviderUserId",
                table: "JobRequests",
                column: "AcceptedProviderUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobRequests_AspNetUsers_AcceptedProviderUserId",
                table: "JobRequests");

            migrationBuilder.DropIndex(
                name: "IX_JobRequests_AcceptedProviderUserId",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "AcceptedProviderUserId",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "JobRequests");
        }
    }
}
