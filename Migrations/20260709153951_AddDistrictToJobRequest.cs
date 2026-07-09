using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddDistrictToJobRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "JobRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "District",
                table: "JobRequests");
        }
    }
}
